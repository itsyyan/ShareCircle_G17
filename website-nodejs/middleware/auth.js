// Authentication middleware
const requireAuth = (req, res, next) => {
    if (req.session && req.session.user) {
        return next();
    }
    // Redirect to login page for browser access
    res.redirect('/login');
};

// Redirect to dashboard if already logged in
const redirectIfAuth = (req, res, next) => {
    if (req.session && req.session.user) {
        return res.redirect('/');
    }
    next();
};

module.exports = {
    requireAuth,
    redirectIfAuth
};
