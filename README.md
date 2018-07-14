# Identity Web Service
A JSON web service to manage user accounts and email

## Concepts
In this documentation a user account is referred to as a _being_.

There is one password associated with each being, which is hashed using SHA-512. The password may
be changed by supplying old and new passwords, or via a password reset token.

Each being is associated with zero or more _clients_. A client is an application which consumes the
web service. When a client associates itself with a being it may store arbitrary data (e.g. a user
ID) in order to give the being meaning within the client's domain. When all of a being's clients
have been deleted, the being is itself deleted.

Beings are not exposed directly by the API. Rather a being is refererenced via one or more
_aliases_, where each alias corresponds to an email address. When an alias is created it may give
rise to a new being or it may be linked to an existing being.

## Example sequence
### Create a new alias with a new being
POST to /aliases/test@test.org:
```json
{
    "password": "7charsmin"
}
```

Response: 204 No Content

### Link a new alias to an existing being
POST to /aliases/test2@test.org:
```json
{
    "otherEmailAddress": "test@test.org"
}
```

Response: 204 No Content

### Change password via old password
PATCH to /aliases/test2@test.org:
```json
{
    "oldPassword": "7charsmin",
    "password":"7charsmin_easy"
}
```

Response: 204 No Content

### Change password via reset token
POST to /aliases/test@test.org/reset with empty body.

Response:
```json
{
    "resetToken": "e40c9502-5350-4ba6-b4d4-0d73e3211d37"
}
```

PATCH to /aliases/test@test.org:
```json
{
    "resetToken": "e40c9502-5350-4ba6-b4d4-0d73e3211d37",
    "password": "different_from_previous"
}
```

Response: 204 No Content

### Remove alias from being
DELETE to /aliases/test2@test.org with empty body.

Response: 204 No Content

### Get token for email confirmation
GET to /aliases/test@test.org

Response:
```json
{
    "confirmToken": "4a0fd45cb6cf417a810c7156f32e6414"
}
```

### Send confirmation email
POST to /aliases/test@test.org/email:
```json
{
    "from": "noreply@test.org",
    "replyTo": "can.be.null@or.absent",
    "subject": "Confirm your email address",
    "bodyText": "Go to https://mycoolapp.com/confirm?token=4a0fd45cb6cf417a810c7156f32e6414",
    "bodyHTML": "If you're not null, undefined or absent, go to <a href=\"https://mycoolapp.com/confirm?token=4a0fd45cb6cf417a810c7156f32e6414\">My Cool App</a>",
    "sendIfUnconfirmed": true
}
```

Note that `sendIfUnconfirmed` if omitted defaults to `false`.

Response: 204 No Content

### Record confirmation
POST to /aliases/test@test.org/confirm:
```json
{
    "confirmToken": "4a0fd45cb6cf417a810c7156f32e6414"
}
```

Response: 204 No Content

### Verify confirmation
GET to /aliases/test@test.org

Response:
```json
{
    "confirmToken": null
}
```

### Add a client
POST to /aliases/test@test.org/clients/testclient:
```json
{
    "arbitrary": "data",
    "string": "string"
}
```

Response: 204 No Content

### Check login details
POST to /aliases/test@test.org/clients/testclient/login:
```json
{
    "password": "different_from_previous"
}
```

Response: 204 No Content

Note that the response status will be 401 Unauthorized if the password is incorrect or 503 Service
Unavailable if too many incorrect requests have been received recently.

### Retrieve client data
GET to /aliases/test@test.org/clients/testclient

Response:
```json
{
    "arbitrary": "data",
    "string": "string"
}
```

### Remove the client (and delete the being)
DELETE to /aliases/test@test.org/clients/testclient with empty body.

Response: 204 No Content

Because there was only one client associated with the being, deleting the client also deletes the
being.

## IdentityWs Client
A client library targeting .NET Standard 1.1 (.NET Framework 4.5 or .NET Core 1.0 and greater) is
available: see [IdentityWs Client](https://github.com/morphologue/IdentityWsClient).

## Security
The web service doesn't require authentication: every request which makes sense will be actioned. As
such the service should only be reachable by trusted applications and should not be exposed
publicly.
