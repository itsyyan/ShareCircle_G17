using Firebase.Database;
using Firebase.Database.Query;
using website.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace website.Services
{
    public class FirebaseService
    {
        private const string FirebaseDatabaseUrl = "https://sharecircle-f335e-default-rtdb.asia-southeast1.firebasedatabase.app/";
        private readonly FirebaseClient _firebaseClient;

        // In-memory cache
        private List<User> _cachedUsers;
        private DateTime _lastUsersFetchTime;
        private List<DonationPost> _cachedPosts;
        private DateTime _lastPostsFetchTime;
        private const int CacheDurationMinutes = 5;

        public FirebaseService()
        {
            _firebaseClient = new FirebaseClient(FirebaseDatabaseUrl);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                var users = await _firebaseClient
                    .Child("users")
                    .OnceAsync<User>();

                return users.Select(u => 
                {
                    var user = u.Object;
                    user.UserId = u.Key; 
                    return user;
                }).ToList();
            }
            catch
            {
                return new List<User>();
            }
        }

        public async Task<List<User>> GetRecentUsersAsync(int count, bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedUsers != null && DateTime.Now < _lastUsersFetchTime.AddMinutes(CacheDurationMinutes))
            {
                return _cachedUsers;
            }

            try
            {
                List<User> result = new List<User>();

                // Strategy 1: Try to order by CreatedAt (requires index)
                try 
                {
                    var users = await _firebaseClient
                        .Child("users")
                        .OrderBy("CreatedAt")
                        .LimitToLast(count)
                        .OnceAsync<User>();

                    var list = users.Select(u => 
                    {
                        var user = u.Object;
                        user.UserId = u.Key; 
                        return user;
                    }).ToList();

                    list.Reverse(); // Newest first
                    result = list;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GetRecentUsersAsync] Strategy 1 failed: {ex.Message}");
                    
                    // Strategy 2: Order by Key (default index)
                    try
                    {
                        var users = await _firebaseClient
                            .Child("users")
                            .OrderByKey()
                            .LimitToLast(count)
                            .OnceAsync<User>();

                        var list = users.Select(u => 
                        {
                            var user = u.Object;
                            user.UserId = u.Key; 
                            return user;
                        }).ToList();

                        list.Reverse();
                        result = list;
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"[GetRecentUsersAsync] Strategy 2 failed: {ex2.Message}");

                        // Strategy 3: Fallback to fetching all (slow but reliable)
                        Console.WriteLine("[GetRecentUsersAsync] Falling back to GetAllUsersAsync");
                        var allUsers = await GetAllUsersAsync();
                        result = allUsers.TakeLast(count).Reverse().ToList();
                    }
                }

                // Update cache
                if (result.Any()) 
                {
                    _cachedUsers = result;
                    _lastUsersFetchTime = DateTime.Now;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetRecentUsersAsync] All strategies failed: {ex.Message}");
                return new List<User>();
            }
        }

        public async Task<List<DonationPost>> GetAllPostsAsync()
        {
            try
            {
                var posts = await _firebaseClient
                    .Child("donations")
                    .OnceAsync<DonationPost>();

                return posts.Select(p => 
                {
                    var post = p.Object;
                    post.PostId = p.Key;
                    return post;
                }).ToList();
            }
            catch
            {
                return new List<DonationPost>();
            }
        }

        public async Task<List<DonationPost>> GetRecentPostsAsync(int count, bool forceRefresh = false)
        {
            if (!forceRefresh && _cachedPosts != null && DateTime.Now < _lastPostsFetchTime.AddMinutes(CacheDurationMinutes))
            {
                return _cachedPosts;
            }

            try
            {
                List<DonationPost> result = new List<DonationPost>();

                // Strategy 1: Try to order by CreatedAt
                try 
                {
                    var posts = await _firebaseClient
                        .Child("donations")
                        .OrderBy("CreatedAt")
                        .LimitToLast(count)
                        .OnceAsync<DonationPost>();

                    var list = posts.Select(p => 
                    {
                        var post = p.Object;
                        post.PostId = p.Key;
                        return post;
                    }).ToList();

                    list.Reverse();
                    result = list;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GetRecentPostsAsync] Strategy 1 failed: {ex.Message}");
                    
                    // Strategy 2: Order by Key
                    try
                    {
                        var posts = await _firebaseClient
                            .Child("donations")
                            .OrderByKey()
                            .LimitToLast(count)
                            .OnceAsync<DonationPost>();

                        var list = posts.Select(p => 
                        {
                            var post = p.Object;
                            post.PostId = p.Key;
                            return post;
                        }).ToList();

                        list.Reverse();
                        result = list;
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"[GetRecentPostsAsync] Strategy 2 failed: {ex2.Message}");

                        // Strategy 3: Fallback to fetching all
                        Console.WriteLine("[GetRecentPostsAsync] Falling back to GetAllPostsAsync");
                        var allPosts = await GetAllPostsAsync();
                        result = allPosts.TakeLast(count).Reverse().ToList();
                    }
                }

                // Update cache
                if (result.Any())
                {
                    _cachedPosts = result;
                    _lastPostsFetchTime = DateTime.Now;
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetRecentPostsAsync] All strategies failed: {ex.Message}");
                return new List<DonationPost>();
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                // Note: Deleting a user from DB doesn't delete from Auth, 
                // but for this admin panel we mainly manage DB records.
                await _firebaseClient.Child("users").Child(userId).DeleteAsync();
                
                // Remove from cache if exists
                if (_cachedUsers != null)
                {
                    var item = _cachedUsers.FirstOrDefault(u => u.UserId == userId);
                    if (item != null)
                    {
                        _cachedUsers.Remove(item);
                    }
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeletePostAsync(string postId)
        {
            try
            {
                await _firebaseClient.Child("donations").Child(postId).DeleteAsync();

                // Remove from cache if exists
                if (_cachedPosts != null)
                {
                    var item = _cachedPosts.FirstOrDefault(p => p.PostId == postId);
                    if (item != null)
                    {
                        _cachedPosts.Remove(item);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> AuthenticateAdminAsync(string username, string password)
        {
            // Fallback for hardcoded admins
            if ((username == "admin" && password == "admin123") || 
                (username == "nwchang" && password == "admin1234"))
            {
                return true;
            }

            try
            {
                var adminNode = await _firebaseClient
                    .Child("admins")
                    .Child(username)
                    .OnceSingleAsync<dynamic>(); // Using dynamic to avoid creating a new class

                if (adminNode != null)
                {
                    // Check if password matches
                    string dbPassword = adminNode.password;
                    return dbPassword == password;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Auth Error: {ex.Message}");
                return false;
            }
        }
    }
}
