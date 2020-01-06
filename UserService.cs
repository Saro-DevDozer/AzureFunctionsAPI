using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UserAPI
{
    public class UserService
    {
        UserDbContext _context;

        public UserService(UserDbContext userDbContext)
        {
            _context = userDbContext;
        }

        public bool ValidateUser(User user)
        {
            if (_context.users.Where(x => x.UserNo == user.UserNo || x.UserName == user.UserName).FirstOrDefault() == null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
