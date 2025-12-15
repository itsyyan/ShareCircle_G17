const express = require('express');
const router = express.Router();
const firebaseService = require('../services/firebaseService');
const { requireAuth } = require('../middleware/auth');

// Posts list page
router.get('/', requireAuth, async (req, res) => {
    try {
        const forceRefresh = req.query.refresh === 'true';
        const posts = await firebaseService.getRecentPosts(50, forceRefresh);

        res.render('posts', {
            title: 'Manage Posts',
            posts: posts,
            message: req.query.message || null
        });
    } catch (error) {
        console.error('Posts page error:', error);
        res.render('posts', {
            title: 'Manage Posts',
            posts: [],
            error: 'Failed to load posts'
        });
    }
});

// Delete post
router.post('/delete/:postId', requireAuth, async (req, res) => {
    const { postId } = req.params;

    if (!postId) {
        return res.redirect('/posts?message=Invalid post ID');
    }

    const success = await firebaseService.deletePost(postId);

    if (success) {
        res.redirect('/posts?message=Post deleted successfully');
    } else {
        res.redirect('/posts?message=Failed to delete post');
    }
});

module.exports = router;
