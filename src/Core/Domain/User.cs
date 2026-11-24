using Microsoft.AspNetCore.Identity;

namespace VideoProcessor.Core.Domain;

/// <summary>A person with an account. Every Video, Job and Output belongs to exactly one User.</summary>
public sealed class User : IdentityUser;
