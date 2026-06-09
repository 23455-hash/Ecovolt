using System.Collections.Generic;
using System.Linq;
using EcoVolt.Models;

namespace EcoVolt
{
    public static class UserStore
    {
        private static readonly List<User> _users = new List<User>
        {
            new User { Name = "Administrador", Email = "admin@ecovolt.local", Password = "Admin123" },
            new User { Name = "Usuario Estándar", Email = "usuario@ecovolt.local", Password = "Usuario123" },
            new User { Name = "Técnico de Medición", Email = "tecnico@ecovolt.local", Password = "Tecnico123" }
        };
        public static User CurrentUser { get; set; }

        public static IReadOnlyList<User> DemoUsers => _users.Take(3).ToList();
        public static void AddUser(User user) => _users.Add(user);
        public static bool EmailExists(string e) => _users.Any(u => u.Email.ToLower() == e.ToLower());
        public static User FindUser(string email, string pwd) =>
            _users.FirstOrDefault(u =>
                u.Email.ToLower() == email.Trim().ToLower() && u.Password == pwd);
    }
}
