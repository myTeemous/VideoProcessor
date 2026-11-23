# Authentication uses ASP.NET Core Identity inside the API

Users register and log in through ASP.NET Core Identity's built-in bearer-token endpoints, with users stored in the same PostgreSQL database. It is self-contained and runs under Docker with no cloud tenant to configure. Every Video is owned by a user, and all queries filter on the owner. Moving to Entra ID later means replacing token issuance and migrating user identities, which is why this is recorded.
