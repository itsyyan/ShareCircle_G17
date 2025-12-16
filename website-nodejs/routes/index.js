const express = require('express');
const router = express.Router();
const firebaseService = require('../services/firebaseService');
const { requireAuth } = require('../middleware/auth');

// Dashboard
router.get('/', requireAuth, async (req, res) => {
    try {
        const users = await firebaseService.getAllUsers();
        const posts = await firebaseService.getAllPosts();

        const totalUsers = users.length;
        const totalPosts = posts.length;

        // New users this month
        const now = new Date();
        const newUsers = users.filter(u => {
            if (!u.CreatedAt) return false;
            const createdAt = new Date(u.CreatedAt);
            return createdAt.getMonth() === now.getMonth() &&
                   createdAt.getFullYear() === now.getFullYear();
        }).length;

        // Top 5 Donors
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

        res.render('index', {
            title: 'Dashboard',
            totalUsers,
            totalPosts,
            newUsers,
            topDonors
        });
    } catch (error) {
        console.error('Dashboard error:', error);
        res.status(500).json({
            error: 'Failed to load statistics',
            details: error.message
        });
    }
});

module.exports = router;
