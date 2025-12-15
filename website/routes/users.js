const express = require('express');
const router = express.Router();
const firebaseService = require('../services/firebaseService');
const { requireAuth } = require('../middleware/auth');

// Users list page
router.get('/', requireAuth, async (req, res) => {
    try {
        const forceRefresh = req.query.refresh === 'true';
        const users = await firebaseService.getRecentUsers(50, forceRefresh);

        res.render('users', {
            title: 'Manage Users',
            users: users,
            message: req.query.message || null
        });
    } catch (error) {
        console.error('Users page error:', error);
        res.render('users', {
            title: 'Manage Users',
            users: [],
            error: 'Failed to load users'
        });
    }
});

// Delete user
router.post('/delete/:userId', requireAuth, async (req, res) => {
    const { userId } = req.params;

    if (!userId) {
        return res.redirect('/users?message=Invalid user ID');
    }

    const success = await firebaseService.deleteUser(userId);

    if (success) {
        res.redirect('/users?message=User deleted successfully');
    } else {
        res.redirect('/users?message=Failed to delete user');
    }
});

module.exports = router;
