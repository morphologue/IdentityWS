using System;

namespace IdentityWs.Jobs
{
    // An entity with a DateCreated field which can be used to determine whether records should be
    // cleaned (deleted)
    public interface ICleanable
    {
        DateTime DateCreated { get; }
    }
}
