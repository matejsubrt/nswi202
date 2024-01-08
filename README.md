# Project overview

## Structure
The project consists of 2 parts - back-end WebAuthn server implemented in .NET and front-end html page with javascript passkeys implementation.

### .NET Server
The implementation uses the Fido2 library by Fido2, that provides implementation of the WebAuthn methods like creating credentials or checking whether credentials are correct.
The code consists of the 4 basic endpoints (functions) needed for implementing passkeys support -> makeCredentialOptions, makeCredential, assertionOptions and makeAssertion, each providing the needed functionality to adhere to specifications.

### Front-end js code
The front end code only contains 2 basic forms for passkey registration and log-in, that use the register() and login() methods on submit respectively. These methods use the server by calling its 4 endpoints to create, register and use passkeys through POST requests.

---------------------------

## Known issues
For some reason, I was not able to get saving the credentials into the user's cookies to work (I was able to correctly save them in the makeCredentialOptions function, but didn't find a way to retrieve the saved data in makeCredentials function), so I created a "mock session" on the server where the credentials are saved instead that has the same interface as the original session would have. I assume this is some kind of technical issue that could probably be fixed without much changes to the code. 

## Testing and development information
I tested and developed the project by running the front-end using mongoose and running the server as a separate .NET application, both on different ports on localhost. I used Windows hello (fingerprint) as the authenticator for the passkey.