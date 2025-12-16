// Firebase REST API Service (无需服务账号)
const https = require('https');

const FIREBASE_DATABASE_URL = 'https://sharecircle-f335e-default-rtdb.asia-southeast1.firebasedatabase.app';

// Cache settings
let cachedUsers = null;
let lastUsersFetchTime = null;
let cachedPosts = null;
let lastPostsFetchTime = null;
const CACHE_DURATION_MINUTES = 5;

// Helper function to make Firebase REST API requests
function firebaseRequest(path, method = 'GET', data = null) {
    return new Promise((resolve, reject) => {
        const url = new URL(`${FIREBASE_DATABASE_URL}${path}.json`);

        const options = {
            hostname: url.hostname,
            path: url.pathname + url.search,
            method: method,
            headers: {
                'Content-Type': 'application/json'
            }
        };

        const req = https.request(options, (res) => {
            let body = '';
            res.on('data', chunk => body += chunk);
            res.on('end', () => {
                try {
                    const result = JSON.parse(body);
                    resolve(result);
                } catch (e) {
                    resolve(null);
                }
            });
        });

        req.on('error', (e) => {
            console.error('Firebase request error:', e);
            reject(e);
        });

        req.setTimeout(10000, () => {
            req.destroy();
            reject(new Error('Request timeout'));
        });

        if (data) {
            req.write(JSON.stringify(data));
        }
        req.end();
    });
}

class FirebaseService {
    // Get all users
    async getAllUsers() {
        try {
            const data = await firebaseRequest('/users');
            if (!data) return [];

            return Object.entries(data).map(([key, value]) => ({
                userId: key,
                ...value
            }));
        } catch (error) {
            console.error('Error fetching users:', error);
            return [];
        }
    }

    // Get recent users with caching
    async getRecentUsers(count = 50, forceRefresh = false) {
        const now = new Date();
        if (!forceRefresh && cachedUsers && lastUsersFetchTime &&
            (now - lastUsersFetchTime) < CACHE_DURATION_MINUTES * 60 * 1000) {
            return cachedUsers;
        }

        try {
            const users = await this.getAllUsers();
            const result = users.slice(-count).reverse();

            cachedUsers = result;
            lastUsersFetchTime = now;
            return result;
        } catch (error) {
            console.error('Error fetching recent users:', error);
            return cachedUsers || [];
        }
    }

    // Get all posts
    async getAllPosts() {
        try {
            const data = await firebaseRequest('/donations');
            if (!data) return [];

            return Object.entries(data).map(([key, value]) => ({
                postId: key,
                ...value
            }));
        } catch (error) {
            console.error('Error fetching posts:', error);
            return [];
        }
    }

    // Get recent posts with caching
    async getRecentPosts(count = 50, forceRefresh = false) {
        const now = new Date();
        if (!forceRefresh && cachedPosts && lastPostsFetchTime &&
            (now - lastPostsFetchTime) < CACHE_DURATION_MINUTES * 60 * 1000) {
            return cachedPosts;
        }

        try {
            const posts = await this.getAllPosts();
            const result = posts.slice(-count).reverse();

            cachedPosts = result;
            lastPostsFetchTime = now;
            return result;
        } catch (error) {
            console.error('Error fetching recent posts:', error);
            return cachedPosts || [];
        }
    }

    // Delete user
    async deleteUser(userId) {
        try {
            await firebaseRequest(`/users/${userId}`, 'DELETE');

            // Update cache
            if (cachedUsers) {
                cachedUsers = cachedUsers.filter(u => u.userId !== userId);
            }
            return true;
        } catch (error) {
            console.error('Error deleting user:', error);
            return false;
        }
    }

    // Delete post
    async deletePost(postId) {
        try {
            await firebaseRequest(`/donations/${postId}`, 'DELETE');

            // Update cache
            if (cachedPosts) {
                cachedPosts = cachedPosts.filter(p => p.postId !== postId);
            }
            return true;
        } catch (error) {
            console.error('Error deleting post:', error);
            return false;
        }
    }

    // Authenticate admin
    async authenticateAdmin(username, password) {
        try {
            const adminData = await firebaseRequest(`/admins/${username}`);
            console.log(`[Auth] Check for ${username}:`, adminData);

            if (adminData) {
                // Handle potential data entry error in DB where key is "password:"
                const dbPassword = adminData.password || adminData['password:'];
                if (dbPassword === password) {
                    return true;
                }
            }
            return false;
        } catch (error) {
            console.error('Auth error:', error);
            return false;
        }
    }
}

module.exports = new FirebaseService();
