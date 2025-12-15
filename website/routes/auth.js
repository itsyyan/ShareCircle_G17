const express = require('express');
const router = express.Router();
const firebaseService = require('../services/firebaseService');
const { redirectIfAuth } = require('../middleware/auth');

// Login page
router.get('/login', redirectIfAuth, (req, res) => {
    res.render('login', {
        layout: false,
        error: null
    });
});

// Login handler
router.post('/login', redirectIfAuth, async (req, res) => {
    const { username, password } = req.body;

    if (!username || !password) {
        return res.render('login', {
            layout: false,
            error: 'Please enter both username and password.'
        });
    }

    const isAuthenticated = await firebaseService.authenticateAdmin(username, password);

    if (isAuthenticated) {
        req.session.user = {
            username: username,
            isAdmin: true
        };
        res.redirect('/');
    } else {
        res.render('login', {
            layout: false,
            error: 'Invalid username or password.'
        });
    }
});

// Logout handler
router.get('/logout', (req, res) => {
    req.session.destroy((err) => {
        if (err) {
            console.error('Error destroying session:', err);
        }
        res.redirect('/login');
    });
});

module.exports = router;
