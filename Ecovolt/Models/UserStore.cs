using System.Collections.Generic;
using System.Linq;
using EcoVolt.Models;

namespace EcoVolt
{
    public static class UserStore
    {
        private static readonly List<User> _users = new List<User>();
        public static User CurrentUser { get; set; }

        public static void AddUser(User user) => _users.Add(user);
        public static bool EmailExists(string e) => _users.Any(u => u.Email.ToLower() == e.ToLower());
        public static User FindUser(string email, string pwd) =>
            _users.FirstOrDefault(u =>
                u.Email.ToLower() == email.Trim().ToLower() && u.Password == pwd);
    }
}