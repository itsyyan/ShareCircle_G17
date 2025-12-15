using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using UserModel = ShareCircle_G17.Models.User;

namespace ShareCircle_G17.Services
{
    public class FirebaseAuthService : IFirebaseAuthService
    {
        // Firebase API Key from google-services.json
        private const string FirebaseApiKey = "AIzaSyDRCg6mrXBWkwSk-XyxVdvu1ZFuzR34gBk";

        private readonly FirebaseAuthClient _authClient;
        private UserCredential? _userCredential;
        
        // We will need to manually instantiate or access the Db service 
        // because of circular dependency potential if we use constructor injection here for database service
        // But for cleaner architecture in MAUI, we can use a service locator pattern or pass it in.
        // However, FirebaseAuthService is usually a singleton and might be initialized before DbService.
        // To keep it simple, we'll instantiate a lightweight FirebaseClient just for this check inside SignInAsync
        // OR better, we can assume that if we are deleted, we shouldn't be able to fetch our profile.

        public FirebaseAuthService()
        {
            var config = new FirebaseAuthConfig
            {
                ApiKey = FirebaseApiKey,
                AuthDomain = "sharecircle-f335e.firebaseapp.com",
                Providers = new FirebaseAuthProvider[]
                {
                    new EmailProvider()
                }
            };

            _authClient = new FirebaseAuthClient(config);
        }

        public async Task<(bool Success, string Message, UserModel? User)> SignUpAsync(string username, string email, string password)
        {
            try
            {
                // Create user with email and password
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password);

                if (userCredential?.User != null)
                {
                    _userCredential = userCredential;

                    var user = new UserModel
                    {
                        UserId = userCredential.User.Uid,
                        Username = username,
                        Email = email,
                        CreatedAt = DateTime.UtcNow
                    };

                    return (true, "Account created successfully!", user);
                }

                return (false, "Failed to create account.", null);
            }
            catch (FirebaseAuthException ex)
            {
                string errorMessage = ex.Reason switch
                {
                    AuthErrorReason.EmailExists => "Email already exists.",
                    AuthErrorReason.WeakPassword => "Password is too weak. Use at least 6 characters.",
                    AuthErrorReason.InvalidEmailAddress => "Invalid email address.",
                    _ => $"Sign up failed: {ex.Message}"
                };

                return (false, errorMessage, null);
            }
            catch (Exception ex)
            {
                return (false, $"An error occurred: {ex.Message}", null);
            }
        }

        public async Task<(bool Success, string Message, UserModel? User)> SignInAsync(string email, string password)
        {
            try
            {
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(email, password);

                if (userCredential?.User != null)
                {
                    // Check if user exists in Realtime Database (i.e. not deleted by Admin)
                    try
                    {
                        var dbUrl = "https://sharecircle-f335e-default-rtdb.asia-southeast1.firebasedatabase.app/";
                        var dbClient = new Firebase.Database.FirebaseClient(dbUrl);
                        var userProfile = await dbClient
                            .Child("users")
                            .Child(userCredential.User.Uid)
                            .OnceSingleAsync<UserModel>();

                        // Check if user profile is null or has no UserId (deleted by Admin)
                        if (userProfile == null || string.IsNullOrEmpty(userProfile.UserId))
                        {
                            // User deleted by Admin
                            _authClient.SignOut();
                            return (false, "This account has been deactivated by an administrator.", null);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Profile check error: {ex.Message}");
                        // Block login if we can't verify user status
                        _authClient.SignOut();
                        return (false, "Unable to verify account status. Please try again.", null);
                    }

                    _userCredential = userCredential;

                    var user = new UserModel
                    {
                        UserId = userCredential.User.Uid,
                        Username = userCredential.User.Info?.DisplayName ?? "User",
                        Email = userCredential.User.Info?.Email ?? email,
                        ProfileImageUrl = userCredential.User.Info?.PhotoUrl
                    };

                    return (true, "Signed in successfully!", user);
                }

                return (false, "Failed to sign in.", null);
            }
            catch (FirebaseAuthException ex)
            {
                string errorMessage = ex.Reason switch
                {
                    AuthErrorReason.WrongPassword => "Incorrect password.",
                    AuthErrorReason.UnknownEmailAddress => "Email not found.",
                    AuthErrorReason.InvalidEmailAddress => "Invalid email address.",
                    AuthErrorReason.TooManyAttemptsTryLater => "Too many failed attempts. Try again later.",
                    _ => $"Sign in failed: {ex.Message}"
                };

                return (false, errorMessage, null);
            }
            catch (Exception ex)
            {
                return (false, $"An error occurred: {ex.Message}", null);
            }
        }

        public async Task SignOutAsync()
        {
            try
            {
                _authClient.SignOut();
                _userCredential = null;
                await Task.CompletedTask;
            }
            catch (Exception)
            {
                // Handle sign out errors if needed
            }
        }

        public void SignOut()
        {
            _authClient.SignOut();
            _userCredential = null;
        }

        public async Task<UserModel?> GetCurrentUserAsync()
        {
            try
            {
                if (_userCredential?.User != null)
                {
                    var user = new UserModel
                    {
                        UserId = _userCredential.User.Uid,
                        Username = _userCredential.User.Info?.DisplayName ?? "User",
                        Email = _userCredential.User.Info?.Email ?? string.Empty,
                        ProfileImageUrl = _userCredential.User.Info?.PhotoUrl
                    };

                    return await Task.FromResult(user);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool IsUserSignedIn()
        {
            return _userCredential?.User != null;
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(string newPassword)
        {
            try
            {
                if (_userCredential?.User == null)
                {
                    return (false, "No user is currently signed in.");
                }

                // Change password using Firebase Auth
                await _userCredential.User.ChangePasswordAsync(newPassword);

                return (true, "Password changed successfully!");
            }
            catch (FirebaseAuthException ex)
            {
                string errorMessage = ex.Reason switch
                {
                    AuthErrorReason.WeakPassword => "Password is too weak. Use at least 6 characters.",
                    _ => $"Failed to change password: {ex.Message}"
                };

                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> DeleteAccountAsync()
        {
            try
            {
                if (_userCredential?.User == null)
                {
                    return (false, "No user is currently signed in.");
                }

                // Delete the user account from Firebase Auth
                await _userCredential.User.DeleteAsync();

                // Clear the current user credential
                _userCredential = null;

                return (true, "Account deleted successfully!");
            }
            catch (FirebaseAuthException ex)
            {
                string errorMessage = $"Failed to delete account: {ex.Message}";
                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                return (false, $"An error occurred: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SendPasswordResetEmailAsync(string email)
        {
            try
            {
                // Validate email format
                if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                {
                    return (false, "Please enter a valid email address.");
                }

                // Send password reset email using Firebase Auth
                await _authClient.ResetEmailPasswordAsync(email);

                return (true, "Password reset email sent! Please check your inbox.");
            }
            catch (FirebaseAuthException ex)
            {
                string errorMessage = ex.Reason switch
                {
                    AuthErrorReason.UnknownEmailAddress => "Email not found. Please check and try again.",
                    AuthErrorReason.InvalidEmailAddress => "Invalid email address.",
                    _ => $"Failed to send reset email: {ex.Message}"
                };

                return (false, errorMessage);
            }
            catch (Exception ex)
            {
                return (false, $"An error occurred: {ex.Message}");
            }
        }
    }
}
