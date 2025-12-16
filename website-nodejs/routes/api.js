const express = require('express');
const router = express.Router();
const firebaseService = require('../services/firebaseService');

// API Authentication middleware
const requireApiAuth = (req, res, next) => {
    if (req.session && req.session.user) {
        return next();
    }
    res.status(401).json({ error: 'Unauthorized' });
};

// Login
router.post('/login', async (req, res) => {
    const { username, password } = req.body;

    if (!username || !password) {
        return res.status(400).json({ error: 'Username and password are required' });
    }

    const isAuthenticated = await firebaseService.authenticateAdmin(username, password);

    if (isAuthenticated) {
        req.session.user = {
            username: username,
            isAdmin: true
        };
        res.json({ success: true, user: { username, isAdmin: true } });
    } else {
        res.status(401).json({ error: 'Invalid username or password' });
    }
});

// Logout
router.post('/logout', (req, res) => {
    req.session = null; // Clear cookie-session
    res.json({ success: true });
});

// Check auth status
router.get('/auth/status', (req, res) => {
    if (req.session && req.session.user) {
        res.json({ authenticated: true, user: req.session.user });
    } else {
        res.json({ authenticated: false });
    }
});

// Dashboard stats
router.get('/dashboard', requireApiAuth, async (req, res) => {
    try {
        const users = await firebaseService.getAllUsers();
        const posts = await firebaseService.getAllPosts();

        const totalUsers = users.length;
        const totalPosts = posts.length;

        const now = new Date();
        const newUsers = users.filter(u => {
            if (!u.CreatedAt) return false;
            const createdAt = new Date(u.CreatedAt);
            return createdAt.getMonth() === now.getMonth() &&
                   createdAt.getFullYear() === now.getFullYear();
        }).length;

        const donorGroups = {};
        posts.forEach(post => {
            if (post.UserId) {
                donorGroups[post.UserId] = (donorGroups[post.UserId] || 0) + 1;
            }
        });

        const topDonors = Object.entries(donorGroups)
            .map(([userId, count]) => {
                const user = users.find(u => u.userId === userId);
                return {
                    name: user?.Username || 'Unknown User',
                    count: count
                };
            })
            .sort((a, b) => b.count - a.count)
            .slice(0, 5);

        res.json({ totalUsers, totalPosts, newUsers, topDonors });
    } catch (error) {
        console.error('Dashboard API error:', error);
        res.status(500).json({ error: 'Failed to load statistics' });
    }
});

// Get users
router.get('/users', requireApiAuth, async (req, res) => {
    try {
        const forceRefresh = req.query.refresh === 'true';
        const users = await firebaseService.getRecentUsers(50, forceRefresh);
        res.json(users);
    } catch (error) {
        console.error('Users API error:', error);
        res.status(500).json({ error: 'Failed to load users' });
    }
});

// Delete user
router.delete('/users/:userId', requireApiAuth, async (req, res) => {
    const { userId } = req.params;

    if (!userId) {
        return res.status(400).json({ error: 'Invalid User ID' });
    }

    const success = await firebaseService.deleteUser(userId);

    if (success) {
        res.json({ success: true, message: 'User deleted successfully' });
    } else {
        res.status(500).json({ error: 'Failed to delete user' });
    }
});

// Get posts
router.get('/posts', requireApiAuth, async (req, res) => {
    try {
        const forceRefresh = req.query.refresh === 'true';
        const posts = await firebaseService.getRecentPosts(50, forceRefresh);
        res.json(posts);
    } catch (error) {
        console.error('Posts API error:', error);
        res.status(500).json({ error: 'Failed to load posts' });
    }
});

// Delete post
router.delete('/posts/:postId', requireApiAuth, async (req, res) => {
    const { postId } = req.params;

    if (!postId) {
        return res.status(400).json({ error: 'Invalid Post ID' });
    }

    const success = await firebaseService.deletePost(postId);

    if (success) {
        res.json({ success: true, message: 'Post deleted successfully' });
    } else {
        res.status(500).json({ error: 'Failed to delete post' });
    }
});

module.exports = router;
