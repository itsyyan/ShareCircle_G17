using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace website.Authentication
{
    public class SimpleAuthStateProvider : AuthenticationStateProvider
    {
        private bool _isAuthenticated;
        private string _username = "";

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = _isAuthenticated
                ? new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, _username),
                    new Claim(ClaimTypes.Role, "Admin")
                }, "apiauth")
                : new ClaimsIdentity();

            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }

        public void AuthenticateUser(string username)
        {
            if (username == "admin" || username == "nwchang") 
            {
                _isAuthenticated = true;
                _username = username;
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            }
        }

        public void Logout()
        {
            _isAuthenticated = false;
            _username = "";
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
