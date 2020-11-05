using BlazorApp.Models.Account;
using BlazorApp.Services;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorApp.Helpers
{
    public class FakeBackendHandler : HttpClientHandler
    {
        private ILocalStorageService _localStorageService;

        public FakeBackendHandler(ILocalStorageService localStorageService)
        {
            _localStorageService = localStorageService;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // array in local storage for registered users
            var usersKey = "blazor-registration-login-example-users";
            var users = await _localStorageService.GetItem<List<UserRecord>>(usersKey) ?? new List<UserRecord>();
            var method = request.Method;
            var path = request.RequestUri.AbsolutePath;            

            return await handleRoute();

            async Task<HttpResponseMessage> handleRoute()
            {
                if (path == "/users/authenticate" && method == HttpMethod.Post)
                    return await authenticate();
                if (path == "/users/register" && method == HttpMethod.Post)
                    return await register();
                if (path == "/users" && method == HttpMethod.Get)
                    return await getUsers();
                if (Regex.Match(path, @"\/users\/\d+$").Success && method == HttpMethod.Get)
                    return await getUserById();
                if (Regex.Match(path, @"\/users\/\d+$").Success && method == HttpMethod.Put)
                    return await updateUser();
                if (Regex.Match(path, @"\/users\/\d+$").Success && method == HttpMethod.Delete)
                    return await deleteUser();
                
                // pass through any requests not handled above
                return await base.SendAsync(request, cancellationToken);
            }

            // route functions
            
            async Task<HttpResponseMessage> authenticate()
            {
                var bodyJson = await request.Content.ReadAsStringAsync();
                var body = JsonSerializer.Deserialize<Login>(bodyJson);
                var user = users.FirstOrDefault(x => x.Username == body.Username && x.Password == body.Password);

                if (user == null)
                    return await error("Username or password is incorrect");

                return await ok(new {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Token = "fake-jwt-token"
                });
            }

            async Task<HttpResponseMessage> register()
            {
                var bodyJson = await request.Content.ReadAsStringAsync();
                var body = JsonSerializer.Deserialize<AddUser>(bodyJson);

                if (users.Any(x => x.Username == body.Username))
                    return await error($"Username '{body.Username}' is already taken");

                var user = new UserRecord {
                    Id = users.Count > 0 ? users.Max(x => x.Id) + 1 : 1,
                    Username = body.Username,
                    Password = body.Password,
                    FirstName = body.FirstName,
                    LastName = body.LastName
                };

                users.Add(user);

                await _localStorageService.SetItem(usersKey, users);
                
                return await ok();
            }

            async Task<HttpResponseMessage> getUsers()
            {
                if (!isLoggedIn()) return await unauthorized();
                return await ok(users.Select(x => basicDetails(x)));
            }

            async Task<HttpResponseMessage> getUserById()
            {
                if (!isLoggedIn()) return await unauthorized();

                var user = users.FirstOrDefault(x => x.Id == idFromPath());
                return await ok(basicDetails(user));
            }

            async Task<HttpResponseMessage> updateUser() 
            {
                if (!isLoggedIn()) return await unauthorized();

                var bodyJson = await request.Content.ReadAsStringAsync();
                var body = JsonSerializer.Deserialize<EditUser>(bodyJson);
                var user = users.FirstOrDefault(x => x.Id == idFromPath());

                // if username changed check it isn't already taken
                if (user.Username != body.Username && users.Any(x => x.Username == body.Username))
                    return await error($"Username '{body.Username}' is already taken");

                // only update password if entered
                if (!string.IsNullOrWhiteSpace(body.Password))
                    user.Password = body.Password;

                // update and save user
                user.Username = body.Username;
                user.FirstName = body.FirstName;
                user.LastName = body.LastName;
                await _localStorageService.SetItem(usersKey, users);

                return await ok();
            }

            async Task<HttpResponseMessage> deleteUser()
            {
                if (!isLoggedIn()) return await unauthorized();

                users.RemoveAll(x => x.Id == idFromPath());
                await _localStorageService.SetItem(usersKey, users);

                return await ok();
            }

            // helper functions

            async Task<HttpResponseMessage> ok(object body = null)
            {
                return await jsonResponse(HttpStatusCode.OK, body ?? new {});
            }

            async Task<HttpResponseMessage> error(string message)
            {
                return await jsonResponse(HttpStatusCode.BadRequest, new { message });
            }

            async Task<HttpResponseMessage> unauthorized()
            {
                return await jsonResponse(HttpStatusCode.Unauthorized, new { message = "Unauthorized" });
            }

            async Task<HttpResponseMessage> jsonResponse(HttpStatusCode statusCode, object content)
            {
                var response = new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(JsonSerializer.Serialize(content), Encoding.UTF8, "application/json")
                };
                
                // delay to simulate real api call
                await Task.Delay(500);

                return response;
            }

            bool isLoggedIn()
            {
                return request.Headers.Authorization?.Parameter == "fake-jwt-token";
            } 

            int idFromPath()
            {
                return int.Parse(path.Split('/').Last());
            }

            dynamic basicDetails(UserRecord user)
            {
                return new {
                    Id = user.Id.ToString(),
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName
                };
            }
        }
    }

    // class for user records stored by fake backend

    public class UserRecord {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}