const { onRequest } = require('firebase-functions/v2/https');
const app = require('./app');

// Export Express app as Firebase Function (2nd gen)
// Set invoker to 'public' to allow unauthenticated access
exports.app = onRequest({
    region: 'asia-southeast1',
    invoker: 'public'
}, app);
