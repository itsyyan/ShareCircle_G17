const express = require('express');
const cookieSession = require('cookie-session');
const cors = require('cors');
const path = require('path');
require('dotenv').config();

const app = express();
const PORT = process.env.APP_PORT || 3000;

// Trust proxy (required for Firebase/Cloud Run)
app.set('trust proxy', 1);

// Enable CORS for frontend
app.use(cors({
    origin: ['http://localhost:5173', 'http://localhost:3001'], // Vite dev ports
    credentials: true
}));

// View engine setup
app.set('view engine', 'ejs');
app.set('views', path.join(__dirname, 'views'));

// Middleware
app.use(express.urlencoded({ extended: true }));
app.use(express.json());
app.use(express.static(path.join(__dirname, 'public')));

// Cookie-based session (works with serverless/Cloud Functions)
app.use(cookieSession({
    name: '__session', // Firebase requires __session cookie name
    keys: [process.env.SESSION_SECRET || 'sharecircle-admin-secret-key'],
    maxAge: 24 * 60 * 60 * 1000, // 24 hours
    secure: true, // Always true for HTTPS
    httpOnly: true,
    sameSite: 'strict'
}));

// Make user available in all templates
app.use((req, res, next) => {
    res.locals.user = req.session.user || null;
    res.locals.currentPath = req.path;
    next();
});

// API Routes (for React frontend)
const apiRoutes = require('./routes/api');
app.use('/api', apiRoutes);

// Legacy EJS Routes (can be removed after migration)
const authRoutes = require('./routes/auth');
const indexRoutes = require('./routes/index');
const usersRoutes = require('./routes/users');
const postsRoutes = require('./routes/posts');

app.use('/', authRoutes);
app.use('/', indexRoutes);
app.use('/users', usersRoutes);
app.use('/posts', postsRoutes);

// 404 handler
app.use((req, res) => {
    res.status(404).render('error', {
        message: 'Page not found',
        error: { status: 404 }
    });
});

// Error handler
app.use((err, req, res, next) => {
    console.error(err.stack);
    res.status(500).render('error', {
        message: 'Something went wrong!',
        error: process.env.NODE_ENV === 'development' ? err : {}
    });
});

// Export app for Firebase Functions
module.exports = app;

// Only start server if not running in Firebase Functions
if (require.main === module) {
    app.listen(PORT, () => {
        console.log(`ShareCircle Admin running on http://localhost:${PORT}`);
    });
}
