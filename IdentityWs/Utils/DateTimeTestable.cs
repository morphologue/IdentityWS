using System;

namespace IdentityWs.Utils
{
    public class DateTimeTestable : IUtcNow
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
