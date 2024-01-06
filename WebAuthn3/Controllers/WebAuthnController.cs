using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Fido2NetLib;
using Fido2NetLib.Development;
using Fido2NetLib.Objects;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using static Fido2NetLib.Fido2;

namespace Fido2Demo;

public static class SessionHelper
{
    private static IDictionary<string, string> Session { get; set; } = new Dictionary<string, string>();

    public static void SetString(string key, string value)
    {
        Session[key] = value;
    }

    public static string GetString(string key)
    {
        return Session.ContainsKey(key) ? Session[key] : string.Empty;
    }
}

[Route("[controller]")]
public class MyController : Controller
{
    private IFido2 fido2;
    public static IMetadataService _mds;
    public static readonly DevelopmentInMemoryStore DemoStorage = new();

    public MyController(IFido2 fido2)
    {
        this.fido2 = fido2;
    }

    private string FormatException(Exception e)
    {
        return string.Format("{0}{1}", e.Message, e.InnerException != null ? " (" + e.InnerException.Message + ")" : "");
    }

    [HttpPost]
    [Route("/makeCredentialOptions")]
    public JsonResult MakeCredentialOptions([FromForm] string username,
                                            [FromForm] string displayName)
    {
        try
        {
            if (string.IsNullOrEmpty(username))
            {
                username = $"{displayName} (Usernameless user created at {DateTime.UtcNow})";
            }

            var user = DemoStorage.GetOrAddUser(username, () => new Fido2User
            {
                DisplayName = displayName,
                Name = username,
                Id = Encoding.UTF8.GetBytes(username) // byte representation of userID is required
            });

            var existingKeys = DemoStorage.GetCredentialsByUser(user).Select(c => c.Descriptor).ToList();

            var authenticatorSelection = AuthenticatorSelection.Default;

            var exts = new AuthenticationExtensionsClientInputs()
            {
                Extensions = true,
                UserVerificationMethod = true
            };

            var options = fido2.RequestNewCredential(user, existingKeys, AuthenticatorSelection.Default, AttestationConveyancePreference.None, exts);

            SessionHelper.SetString("fido2.attestationOptions", options.ToJson());

            var checkValue = SessionHelper.GetString("fido2.attestationOptions"); // For debugging
            Console.WriteLine(checkValue);


            return Json(options);
        }
        catch (Exception e)
        {
            return Json(new CredentialCreateOptions { Status = "error", ErrorMessage = FormatException(e) });
        }
    }
    [HttpPost]
    [Route("/makeCredential")]
    public async Task<JsonResult> MakeCredential([FromBody] AuthenticatorAttestationRawResponse attestationResponse, CancellationToken cancellationToken)
    {
        try
        {
            var jsonOptions = SessionHelper.GetString("fido2.attestationOptions");
            var options = CredentialCreateOptions.FromJson(jsonOptions);

            IsCredentialIdUniqueToUserAsyncDelegate callback = static async (args, cancellationToken) =>
            {
                var users = await DemoStorage.GetUsersByCredentialIdAsync(args.CredentialId, cancellationToken);
                if (users.Count > 0)
                    return false;

                return true;
            };

            var success = await fido2.MakeNewCredentialAsync(attestationResponse, options, callback, cancellationToken: cancellationToken);

            DemoStorage.AddCredentialToUser(options.User, new StoredCredential
            {
                UserId = success.Result.User.Id,
                Descriptor = new PublicKeyCredentialDescriptor(success.Result.CredentialId),
                PublicKey = success.Result.PublicKey,
                UserHandle = success.Result.User.Id,
                SignatureCounter = success.Result.Counter,
                RegDate = DateTime.UtcNow,
                AaGuid = success.Result.Aaguid
            });

            return Json(success);
        }
        catch (Exception e)
        {
            return Json(new CredentialMakeResult(status: "error", errorMessage: FormatException(e), result: null));
        }
    }
    [HttpPost]
    [Route("/assertionOptions")]
    public ActionResult AssertionOptionsPost([FromForm] string username)
    {
        try
        {
            var existingCredentials = new List<PublicKeyCredentialDescriptor>();

            if (!string.IsNullOrEmpty(username))
            {
                var user = DemoStorage.GetUser(username) ?? throw new ArgumentException("Username was not registered");

                existingCredentials = DemoStorage.GetCredentialsByUser(user).Select(c => c.Descriptor).ToList();
            }

            var exts = new AuthenticationExtensionsClientInputs()
            {
                Extensions = true,
                UserVerificationMethod = true
            };

            var uv = UserVerificationRequirement.Preferred;
            var options = fido2.GetAssertionOptions(
                existingCredentials,
                uv,
                exts
            );

            SessionHelper.SetString("fido2.assertionOptions", options.ToJson());

            return Json(options);
        }

        catch (Exception e)
        {
            return Json(new AssertionOptions { Status = "error", ErrorMessage = FormatException(e) });
        }
    }

    [HttpPost]
    [Route("/makeAssertion")]
    public async Task<JsonResult> MakeAssertion([FromBody] AuthenticatorAssertionRawResponse clientResponse, CancellationToken cancellationToken)
    {
        try
        {
            var jsonOptions = SessionHelper.GetString("fido2.assertionOptions");
            var options = AssertionOptions.FromJson(jsonOptions);


            var x = DemoStorage;

            var creds = DemoStorage.GetCredentialById(clientResponse.Id);

            if(creds == null)
            {
                throw new Exception("Unknown credentials");
            }

            var storedCounter = creds.SignatureCounter;

            IsUserHandleOwnerOfCredentialIdAsync callback = static async (args, cancellationToken) =>
            {
                var storedCreds = await DemoStorage.GetCredentialsByUserHandleAsync(args.UserHandle, cancellationToken);
                return storedCreds.Exists(c => c.Descriptor.Id.SequenceEqual(args.CredentialId));
            };

            var res = await fido2.MakeAssertionAsync(clientResponse, options, creds.PublicKey, storedCounter, callback, cancellationToken: cancellationToken);

            DemoStorage.UpdateCounter(res.CredentialId, res.Counter);


            return Json(res);
        }
        catch (Exception e)
        {
            return Json(new AssertionOptions { Status = "error", ErrorMessage = FormatException(e) });
        }
    }
}
