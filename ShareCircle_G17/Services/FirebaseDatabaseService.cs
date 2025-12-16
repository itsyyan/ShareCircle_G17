using Firebase.Database;
using Firebase.Database.Query;
using ShareCircle_G17.Models;

namespace ShareCircle_G17.Services
{
    public class FirebaseDatabaseService : IFirebaseDatabaseService
    {
        // Firebase Realtime Database URL (region: asia-southeast1)
        private const string FirebaseDatabaseUrl = "https://sharecircle-f335e-default-rtdb.asia-southeast1.firebasedatabase.app/";

        private readonly FirebaseClient _firebaseClient;

        public FirebaseDatabaseService()
        {
            _firebaseClient = new FirebaseClient(FirebaseDatabaseUrl);
        }

        public void ListenForNotifications(string userId)
        {
            // Placeholder for notification listener - currently disabled to resolve build conflicts
        }

        #region Donation Posts Operations

        public async Task<(bool Success, string Message, string? PostId)> CreateDonationPostAsync(DonationPost post)
        {
            try
            {
                // Only generate PostId if not already set
                if (string.IsNullOrEmpty(post.PostId))
                {
                    post.PostId = Guid.NewGuid().ToString();
                }

                // Only set CreatedAt if not already set
                if (post.CreatedAt == default || post.CreatedAt == DateTime.MinValue)
                {
                    post.CreatedAt = DateTime.UtcNow;
                }

                // Set default values if not set
                if (string.IsNullOrEmpty(post.Status))
                {
                    post.Status = "Available";
                }

                await _firebaseClient
                    .Child("donations")
                    .Child(post.PostId)
                    .PutAsync(post);

                return (true, "Donation post created successfully!", post.PostId);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to create post: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateDonationPostAsync(string postId, DonationPost post)
        {
            try
            {
                post.UpdatedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child("donations")
                    .Child(postId)
                    .PutAsync(post);

                return (true, "Post updated successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to update post: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteDonationPostAsync(string postId)
        {
            try
            {
                await _firebaseClient
                    .Child("donations")
                    .Child(postId)
                    .DeleteAsync();

                return (true, "Post deleted successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete post: {ex.Message}");
            }
        }

        public async Task<DonationPost?> GetDonationPostAsync(string postId)
        {
            try
            {
                var post = await _firebaseClient
                    .Child("donations")
                    .Child(postId)
                    .OnceSingleAsync<DonationPost>();

                return post;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<DonationPost>?> GetAllDonationPostsAsync()
        {
            try
            {
                var posts = await _firebaseClient
                    .Child("donations")
                    .OnceAsync<DonationPost>();

                return posts
                    .Select(p => new DonationPost
                    {
                        PostId = p.Key,
                        UserId = p.Object.UserId,
                        Username = p.Object.Username,
                        UserImageUrl = p.Object.UserImageUrl,
                        Title = p.Object.Title,
                        Description = p.Object.Description,
                        Category = p.Object.Category,
                        ImageUrl = p.Object.ImageUrl,
                        Latitude = p.Object.Latitude,
                        Longitude = p.Object.Longitude,
                        Location = p.Object.Location,
                        CreatedAt = p.Object.CreatedAt,
                        UpdatedAt = p.Object.UpdatedAt,
                        Status = p.Object.Status,
                        ViewCount = p.Object.ViewCount,
                        SubCategory = p.Object.SubCategory,
                        DropOffLocation = p.Object.DropOffLocation,
                        ContactEmail = p.Object.ContactEmail,
                        IsSynced = true
                    })
                    .Where(p => string.Equals(p.Status, "Available", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<DonationPost>> GetUserDonationPostsAsync(string userId)
        {
            try
            {
                List<DonationPost> userPosts = new List<DonationPost>();

                // Try querying with OrderBy/EqualTo first (requires Firebase index)
                try
                {
                    var posts = await _firebaseClient
                        .Child("donations")
                        .OrderBy("UserId")
                        .EqualTo(userId)
                        .OnceAsync<DonationPost>();

                    if (posts != null && posts.Count > 0)
                    {
                        userPosts = posts
                            .Select(p => new DonationPost
                            {
                                PostId = p.Key,
                                UserId = p.Object.UserId,
                                Username = p.Object.Username,
                                UserImageUrl = p.Object.UserImageUrl,
                                Title = p.Object.Title,
                                Description = p.Object.Description,
                                Category = p.Object.Category,
                                ImageUrl = p.Object.ImageUrl,
                                Latitude = p.Object.Latitude,
                                Longitude = p.Object.Longitude,
                                Location = p.Object.Location,
                                CreatedAt = p.Object.CreatedAt,
                                UpdatedAt = p.Object.UpdatedAt,
                                Status = p.Object.Status,
                                ViewCount = p.Object.ViewCount,
                                SubCategory = p.Object.SubCategory,
                                DropOffLocation = p.Object.DropOffLocation,
                                ContactEmail = p.Object.ContactEmail,
                                IsSynced = true
                            })
                            .OrderByDescending(p => p.CreatedAt)
                            .ToList();

                        return userPosts;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"OrderBy query failed (index may not exist): {ex.Message}");
                    // Fall through to fetch all and filter locally
                }

                // Fallback: Get all donations and filter locally
                System.Diagnostics.Debug.WriteLine("Using fallback: fetching all donations and filtering locally");
                var allPosts = await _firebaseClient
                    .Child("donations")
                    .OnceAsync<DonationPost>();

                System.Diagnostics.Debug.WriteLine($"GetUserDonationPostsAsync - Total posts: {allPosts?.Count ?? 0}");

                if (allPosts != null)
                {
                    userPosts = allPosts
                        .Where(p => p.Object != null && p.Object.UserId == userId)
                        .Select(p => new DonationPost
                        {
                            PostId = p.Key,
                            UserId = p.Object.UserId,
                            Username = p.Object.Username,
                            UserImageUrl = p.Object.UserImageUrl,
                            Title = p.Object.Title,
                            Description = p.Object.Description,
                            Category = p.Object.Category,
                            ImageUrl = p.Object.ImageUrl,
                            Latitude = p.Object.Latitude,
                            Longitude = p.Object.Longitude,
                            Location = p.Object.Location,
                            CreatedAt = p.Object.CreatedAt,
                            UpdatedAt = p.Object.UpdatedAt,
                            Status = p.Object.Status,
                            ViewCount = p.Object.ViewCount,
                            SubCategory = p.Object.SubCategory,
                            DropOffLocation = p.Object.DropOffLocation,
                            ContactEmail = p.Object.ContactEmail,
                            IsSynced = true
                        })
                        .OrderByDescending(p => p.CreatedAt)
                        .ToList();
                }

                System.Diagnostics.Debug.WriteLine($"GetUserDonationPostsAsync - Filtered posts: {userPosts.Count} for userId: {userId}");

                return userPosts;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserDonationPostsAsync - Error: {ex.Message}");
                return new List<DonationPost>();
            }
        }

        #endregion

        #region User Profile Operations

        public async Task<(bool Success, string Message)> SaveUserProfileAsync(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(user.UserId))
                {
                    return (false, "User ID is required.");
                }

                user.UsernameLower = user.Username?.ToLowerInvariant();

                await _firebaseClient
                    .Child("users")
                    .Child(user.UserId)
                    .PutAsync(user);

                return (true, "User profile saved successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to save user profile: {ex.Message}");
            }
        }

        public async Task<User?> GetUserProfileAsync(string userId)
        {
            try
            {
                var user = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .OnceSingleAsync<User>();

                if (user != null && string.IsNullOrEmpty(user.UserId))
                {
                    user.UserId = userId;
                }

                return user;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<User?> GetUserProfileByUsernameAsync(string username)
        {
            var normalized = username.Trim().ToLowerInvariant();
            // Try multiple strategies; do not bail out on first failure to handle missing indexes/rules.
            User? candidate = null;

            // 1) Preferred: query by normalized field
            try
            {
                var users = await _firebaseClient
                    .Child("users")
                    .OrderBy("UsernameLower")
                    .EqualTo(normalized)
                    .OnceAsync<User>();

                candidate = users.FirstOrDefault()?.Object;
                if (candidate != null && string.IsNullOrEmpty(candidate.UserId))
                {
                    candidate.UserId = users.First().Key;
                }
            }
            catch { /* ignore and try fallback */ }

            // 2) Fallback: query by raw Username (legacy records)
            if (candidate == null)
            {
                try
                {
                    var users = await _firebaseClient
                        .Child("users")
                        .OrderBy("Username")
                        .EqualTo(username)
                        .OnceAsync<User>();

                    var match = users.FirstOrDefault();
                    if (match != null)
                    {
                        candidate = match.Object;
                        if (candidate != null && string.IsNullOrEmpty(candidate.UserId))
                        {
                            candidate.UserId = match.Key;
                        }
                    }
                }
                catch { /* ignore and try fallback scan */ }
            }

            // 3) Final fallback: scan all users (case-insensitive match)
            if (candidate == null)
            {
                try
                {
                    var allUsers = await _firebaseClient
                        .Child("users")
                        .OnceAsync<User>();

                    var match = allUsers?
                        .FirstOrDefault(u =>
                            u.Object != null &&
                            !string.IsNullOrWhiteSpace(u.Object.Username) &&
                            string.Equals(u.Object.Username.Trim(), username, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        candidate = match.Object;
                        if (candidate != null && string.IsNullOrEmpty(candidate.UserId))
                        {
                            candidate.UserId = match.Key;
                        }
                    }
                }
                catch { /* give up */ }
            }

            return candidate;
        }

        public async Task<bool> IsUsernameTakenAsync(string username, string? excludeUserId = null)
        {
            var normalized = username.Trim().ToLowerInvariant();

            // 1) Try normalized field
            try
            {
                var matches = await _firebaseClient
                    .Child("users")
                    .OrderBy("UsernameLower")
                    .EqualTo(normalized)
                    .OnceAsync<User>();

                if (matches != null && matches.Any(m => m.Key != excludeUserId))
                {
                    return true;
                }
            }
            catch { /* ignore and try fallback */ }

            // 2) Fallback legacy field
            try
            {
                var legacyMatches = await _firebaseClient
                    .Child("users")
                    .OrderBy("Username")
                    .EqualTo(username)
                    .OnceAsync<User>();

                if (legacyMatches != null && legacyMatches.Any(m => m.Key != excludeUserId))
                {
                    return true;
                }
            }
            catch { /* ignore and try scan */ }

            // 3) Final fallback: scan all users
            try
            {
                var allUsers = await _firebaseClient
                    .Child("users")
                    .OnceAsync<User>();

                if (allUsers != null &&
                    allUsers.Any(u =>
                        u.Key != excludeUserId &&
                        u.Object != null &&
                        !string.IsNullOrWhiteSpace(u.Object.Username) &&
                        string.Equals(u.Object.Username.Trim(), username, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }

        public async Task<(bool Success, string Message)> UpdateUserProfileAsync(User user)
        {
            try
            {
                if (string.IsNullOrEmpty(user.UserId))
                {
                    return (false, "User ID is required.");
                }

                user.UsernameLower = user.Username?.ToLowerInvariant();

                await _firebaseClient
                    .Child("users")
                    .Child(user.UserId)
                    .PutAsync(user);

                return (true, "User profile updated successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to update user profile: {ex.Message}");
            }
        }

        #endregion

        #region Address Book Operations

        public async Task<List<Address>> GetAddressesAsync(string userId)
        {
            try
            {
                var results = await _firebaseClient
                    .Child("addresses")
                    .Child(userId)
                    .OnceAsync<Address>();

                var addresses = results?
                    .Where(a => a.Object != null)
                    .Select(a =>
                    {
                        var addr = a.Object;
                        addr.AddressId ??= a.Key;
                        addr.UserId ??= userId;
                        return addr;
                    })
                    .OrderByDescending(a => a.CreatedAt)
                    .ToList();

                return addresses ?? new List<Address>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetAddressesAsync error: {ex.Message}");
                return new List<Address>();
            }
        }

        public async Task<(bool Success, string Message, string? AddressId)> SaveAddressAsync(string userId, Address address)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return (false, "User ID is required.", null);

                if (string.IsNullOrWhiteSpace(address.AddressId))
                {
                    address.AddressId = Guid.NewGuid().ToString();
                }

                address.UserId = userId;
                if (address.CreatedAt == default || address.CreatedAt == DateTime.MinValue)
                {
                    address.CreatedAt = DateTime.UtcNow;
                }

                await _firebaseClient
                    .Child("addresses")
                    .Child(userId)
                    .Child(address.AddressId)
                    .PutAsync(address);

                return (true, "Address saved.", address.AddressId);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to save address: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message)> DeleteAddressAsync(string userId, string addressId)
        {
            try
            {
                await _firebaseClient
                    .Child("addresses")
                    .Child(userId)
                    .Child(addressId)
                    .DeleteAsync();

                return (true, "Address deleted.");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete address: {ex.Message}");
            }
        }

        #endregion

        #region Saved Items Operations

        public async Task<List<DonationPost>> GetSavedItemsAsync(string userId)
        {
            try
            {
                var savedItems = await _firebaseClient
                    .Child("savedItems")
                    .Child(userId)
                    .OnceAsync<object>();

                if (savedItems == null || !savedItems.Any())
                {
                    return new List<DonationPost>();
                }

                // Keys are postIds; value might be true or postId (legacy)
                var postIds = savedItems
                    .Select(item => string.IsNullOrWhiteSpace(item.Key)
                        ? item.Object?.ToString()
                        : item.Key)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var posts = new List<DonationPost>();
                foreach (var postId in postIds)
                {
                    var post = await GetDonationPostAsync(postId);
                    if (post != null)
                    {
                        posts.Add(post);
                    }
                }

                return posts.OrderByDescending(p => p.CreatedAt).ToList();
            }
            catch (Exception)
            {
                return new List<DonationPost>();
            }
        }

        public async Task<(bool Success, string Message)> SaveItemAsync(string userId, string postId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(postId))
                {
                    return (false, "User ID and Post ID are required.");
                }

                // Save a lightweight marker (bool) under user's saved items keyed by postId
                await _firebaseClient
                    .Child("savedItems")
                    .Child(userId)
                    .Child(postId)
                    .PutAsync(true);

                return (true, "Item saved successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to save item: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> UnsaveItemAsync(string userId, string postId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(postId))
                {
                    return (false, "User ID and Post ID are required.");
                }

                // Remove the post ID from user's saved items
                await _firebaseClient
                    .Child("savedItems")
                    .Child(userId)
                    .Child(postId)
                    .DeleteAsync();

                return (true, "Item unsaved successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to unsave item: {ex.Message}");
            }
        }

        #endregion

        #region Donation Request Operations

        public async Task<(bool Success, string Message, string? RequestId)> CreateDonationRequestAsync(DonationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.RequestId))
                {
                    request.RequestId = Guid.NewGuid().ToString();
                }

                if (request.RequestedAt == default || request.RequestedAt == DateTime.MinValue)
                {
                    request.RequestedAt = DateTime.UtcNow;
                }

                if (string.IsNullOrEmpty(request.Status))
                {
                    request.Status = "Pending";
                }

                // 1. Check if Post is Available
                if (!string.IsNullOrEmpty(request.PostId))
                {
                    var post = await GetDonationPostAsync(request.PostId);
                    if (post == null)
                    {
                        return (false, "Item not found.", null);
                    }

                    if (!string.Equals(post.Status, "Available", StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, "Item is no longer available.", null);
                    }

                    // 2. Lock the post (Set to Pending)
                    post.Status = "Pending";
                    post.UpdatedAt = DateTime.UtcNow;
                    
                    // We update the post first to prevent race conditions (simple optimistic locking)
                    // In a real transactional system, this should be atomic.
                    await _firebaseClient
                        .Child("donations")
                        .Child(request.PostId)
                        .PutAsync(post);
                }

                // 3. Create Request
                await _firebaseClient
                    .Child("requests")
                    .Child(request.RequestId)
                    .PutAsync(request);

                // Also write a lightweight notification entry for the donor (if provided)
                if (!string.IsNullOrWhiteSpace(request.DonorId))
                {
                    // Ensure Requester's username is present for notification
                    var requesterUsername = request.RequesterName;
                    if (string.IsNullOrWhiteSpace(requesterUsername) && !string.IsNullOrWhiteSpace(request.RequesterId))
                    {
                        requesterUsername = await GetUsernameForUserId(request.RequesterId);
                    }

                    var notification = new UserNotification
                    {
                        NotificationId = request.RequestId,
                        Type = "request",
                        RequestId = request.RequestId,
                        PostId = request.PostId,
                        FromUserId = request.RequesterId,
                        ItemTitle = request.ItemTitle,
                        ItemImageUrl = request.ItemImageUrl,
                        Status = request.Status,
                        Message = request.Message,
                        CreatedAt = request.RequestedAt,
                        IsRead = false
                    };

                    await _firebaseClient
                        .Child("notifications")
                        .Child(request.DonorId)
                        .Child(notification.NotificationId)
                        .PutAsync(notification);
                }

                return (true, "Request created successfully!", request.RequestId);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to create request: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateRequestStatusAsync(string requestId, string status)
        {
            try
            {
                var request = await GetRequestAsync(requestId);
                if (request == null)
                {
                    return (false, "Request not found.");
                }

                // 1. Handle Post Status Update FIRST (to prevent inconsistency)
                if (!string.IsNullOrEmpty(request.PostId))
                {
                    var post = await GetDonationPostAsync(request.PostId);
                    if (post != null)
                    {
                        bool postUpdated = false;

                        // If approving/completing, mark post as Completed
                        if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                        {
                            post.Status = "Completed";
                            postUpdated = true;
                        }
                        // If rejecting/cancelling, unlock the post (make Available)
                        else if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase))
                        {
                            post.Status = "Available";
                            postUpdated = true;
                        }

                        if (postUpdated)
                        {
                            await UpdateDonationPostAsync(request.PostId, post);
                        }
                    }
                }

                // 2. Update Request Status
                request.Status = status;
                request.RespondedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child("requests")
                    .Child(requestId)
                    .PutAsync(request);

                // 3. Send Notification
                if (!string.IsNullOrEmpty(request.RequesterId))
                {
                    // Ensure Donor's username is present for notification
                    var donorUsername = request.DonorName;
                    if (string.IsNullOrWhiteSpace(donorUsername) && !string.IsNullOrWhiteSpace(request.DonorId))
                    {
                        donorUsername = await GetUsernameForUserId(request.DonorId);
                    }

                    var notification = new UserNotification
                    {
                        NotificationId = Guid.NewGuid().ToString(),
                        Type = "status_update",
                        RequestId = request.RequestId,
                        PostId = request.PostId,
                        FromUserId = request.DonorId,
                        ItemTitle = request.ItemTitle,
                        ItemImageUrl = request.ItemImageUrl,
                        Status = status,
                        Message = $"Your request for {request.ItemTitle} has been {status}.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    await _firebaseClient
                        .Child("notifications")
                        .Child(request.RequesterId)
                        .Child(notification.NotificationId)
                        .PutAsync(notification);
                }

                return (true, "Request status updated successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to update request status: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> CancelRequestAsync(string requestId)
        {
            try
            {
                var request = await GetRequestAsync(requestId);
                if (request == null)
                {
                    return (false, "Request not found.");
                }

                request.Status = "Cancelled";
                request.RespondedAt = DateTime.UtcNow;

                await _firebaseClient
                    .Child("requests")
                    .Child(requestId)
                    .PutAsync(request);

                // Revert post to Available
                if (!string.IsNullOrEmpty(request.PostId))
                {
                    var post = await GetDonationPostAsync(request.PostId);
                    if (post != null)
                    {
                        post.Status = "Available";
                        await UpdateDonationPostAsync(request.PostId, post);
                    }
                }

                return (true, "Request cancelled successfully!");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to cancel request: {ex.Message}");
            }
        }

        public async Task<DonationRequest?> GetRequestAsync(string requestId)
        {
            try
            {
                var request = await _firebaseClient
                    .Child("requests")
                    .Child(requestId)
                    .OnceSingleAsync<DonationRequest>();

                return request;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<DonationRequest>> GetUserRequestsAsync(string userId)
        {
            try
            {
                var allRequests = await _firebaseClient
                    .Child("requests")
                    .OnceAsync<DonationRequest>();

                var userRequests = allRequests?
                    .Where(r => r.Object != null && r.Object.RequesterId == userId)
                    .Select(r => new DonationRequest
                    {
                        RequestId = r.Key,
                        PostId = r.Object.PostId,
                        RequesterId = r.Object.RequesterId,
                        RequesterName = r.Object.RequesterName,
                        RequesterEmail = r.Object.RequesterEmail,
                        DonorId = r.Object.DonorId,
                        DonorName = r.Object.DonorName,
                        ItemTitle = r.Object.ItemTitle,
                        ItemDescription = r.Object.ItemDescription,
                        ItemImageUrl = r.Object.ItemImageUrl,
                        Category = r.Object.Category,
                        SubCategory = r.Object.SubCategory,
                        Message = r.Object.Message,
                        Status = r.Object.Status,
                        RequestedAt = r.Object.RequestedAt,
                        RespondedAt = r.Object.RespondedAt,
                        IsSynced = true
                    })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                return userRequests ?? new List<DonationRequest>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetUserRequestsAsync - Error: {ex.Message}");
                return new List<DonationRequest>();
            }
        }

        public async Task<List<DonationRequest>> GetDonationRequestsForPostAsync(string postId)
        {
            try
            {
                var allRequests = await _firebaseClient
                    .Child("requests")
                    .OnceAsync<DonationRequest>();

                var postRequests = allRequests?
                    .Where(r => r.Object != null && r.Object.PostId == postId)
                    .Select(r => new DonationRequest
                    {
                        RequestId = r.Key,
                        PostId = r.Object.PostId,
                        RequesterId = r.Object.RequesterId,
                        RequesterName = r.Object.RequesterName,
                        RequesterEmail = r.Object.RequesterEmail,
                        DonorId = r.Object.DonorId,
                        DonorName = r.Object.DonorName,
                        ItemTitle = r.Object.ItemTitle,
                        ItemDescription = r.Object.ItemDescription,
                        ItemImageUrl = r.Object.ItemImageUrl,
                        Category = r.Object.Category,
                        SubCategory = r.Object.SubCategory,
                        Message = r.Object.Message,
                        Status = r.Object.Status,
                        RequestedAt = r.Object.RequestedAt,
                        RespondedAt = r.Object.RespondedAt,
                        IsSynced = true
                    })
                    .OrderByDescending(r => r.RequestedAt)
                    .ToList();

                return postRequests ?? new List<DonationRequest>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetDonationRequestsForPostAsync - Error: {ex.Message}");
                return new List<DonationRequest>();
            }
        }

        #endregion

        #region Notifications

        public async Task<List<UserNotification>> GetNotificationsAsync(string userId)
        {
            var list = new List<UserNotification>();
            try
            {
                var results = await _firebaseClient
                    .Child("notifications")
                    .Child(userId)
                    .OnceAsync<UserNotification>();

                if (results != null)
                {
                    foreach (var item in results)
                    {
                        var n = item.Object ?? new UserNotification();
                        n.NotificationId ??= item.Key;
                        list.Add(n);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetNotificationsAsync error: {ex.Message}");
            }

            return list
                .OrderByDescending(n => n.CreatedAt == default ? DateTime.MinValue : n.CreatedAt)
                .ToList();
        }

        public async Task<(bool Success, string Message)> DeleteNotificationAsync(string userId, string notificationId)
        {
            try
            {
                await _firebaseClient
                    .Child("notifications")
                    .Child(userId)
                    .Child(notificationId)
                    .DeleteAsync();

                return (true, "Deleted");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to delete notification: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> MarkNotificationAsReadAsync(string userId, string notificationId)
        {
            try
            {
                await _firebaseClient
                    .Child("notifications")
                    .Child(userId)
                    .Child(notificationId)
                    .Child("IsRead")
                    .PutAsync(true);
                return (true, "Marked as read");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to mark as read: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> MarkAllNotificationsAsReadAsync(string userId)
        {
            try
            {
                var notifications = await GetNotificationsAsync(userId);
                var unread = notifications.Where(n => !n.IsRead && !string.IsNullOrEmpty(n.NotificationId)).ToList();
                
                // Process in parallel for speed
                var tasks = unread.Select(n => MarkNotificationAsReadAsync(userId, n.NotificationId!));
                await Task.WhenAll(tasks);

                return (true, "All marked as read");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to mark all as read: {ex.Message}");
            }
        }

        #endregion

        #region Developer / Testing

        public async Task<(bool Success, string Message)> ResetDatabaseAsync()
        {
            try
            {
                // Delete all major collections
                await _firebaseClient.Child("donations").DeleteAsync();
                await _firebaseClient.Child("users").DeleteAsync();
                await _firebaseClient.Child("requests").DeleteAsync();
                await _firebaseClient.Child("addresses").DeleteAsync();
                await _firebaseClient.Child("savedItems").DeleteAsync();
                await _firebaseClient.Child("notifications").DeleteAsync();

                return (true, "All data has been reset.");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to reset database: {ex.Message}");
            }
        }

        public IObservable<bool> ListenForUserDeletion(string userId)
        {
            // Create an observable stream that emits true if the user node becomes null (deleted)
            return System.Reactive.Linq.Observable.Create<bool>(observer =>
            {
                var subscription = _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .AsObservable<User>()
                    .Subscribe(e =>
                    {
                        // e.Object is null when the data is deleted
                        if (e.Object == null)
                        {
                            observer.OnNext(true); // User deleted
                        }
                    },
                    error => 
                    {
                        System.Diagnostics.Debug.WriteLine($"ListenForUserDeletion error: {error.Message}");
                    });

                return () => subscription.Dispose();
            });
        }

        private async Task<string> GetUsernameForUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return "User";
            var userProfile = await GetUserProfileAsync(userId);
            return userProfile?.Username ?? "User";
        }

        #endregion
    }
}
