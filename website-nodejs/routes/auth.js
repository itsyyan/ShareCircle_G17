const express = require('express');
const router = express.Router();
const firebaseService = require('../services/firebaseService');

// Login page
router.get('/login', (req, res) => {
    // If already logged in, redirect to dashboard
    if (req.session.user) {
        return res.redirect('/');
    }
    res.render('login', { error: null });
});

// Login handler
router.post('/login', async (req, res) => {
    const { username, password } = req.body;

    if (!username || !password) {
        return res.render('login', { error: 'Please enter both username and password.' });
    }

    const isAuthenticated = await firebaseService.authenticateAdmin(username, password);

    if (isAuthenticated) {
        req.session.user = {
            username: username,
            isAdmin: true
        };
        res.redirect('/');
    } else {
        res.render('login', { error: 'Invalid username or password.' });
    }
});

// Logout handler
router.post('/logout', (req, res) => {
    req.session = null; // Clear cookie-session
    res.redirect('/login');
});

module.exports = router;
