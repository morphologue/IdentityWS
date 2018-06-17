using System;

namespace IdentityWS.Utils
{
    public interface IUtcNow
    {
        DateTime UtcNow { get; }
    }

    public class DateTimeTestable : IUtcNow
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
