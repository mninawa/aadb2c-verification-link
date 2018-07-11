# Custom email verification with confirmation link
This sample solution demonstrates how to use custom email verification. The custom email verification solution allows you to send your own custom email verification during sign-up or password reset user journey. The solution required using Azure AD B2C custom policy and a REST API endpoint that sends the email verification and validate the client assertion.

The key concept of custom email verification: During sign-up or password reset, custom policy calls a REST API endpoint that generates a link to back to the Azure AD B2C policy with an email address to be validated. The link contains the email address. The email verification code is encapsulated inside a JWT token, and sends as client assertion back to Azure AD B2C. When a user clicks on that link, Azure AD B2C checks the JWT token signature, reads the information from the JTW token, and extracts the email address claim. The policy trusts and uses the email address to continue the sign-up and password reset flow.


## Disclaimer
The sample solution is developed and managed by the open-source community in GitHub. The solution is not part of Azure AD B2C product and it's not supported under any Microsoft standard support program or service. The solution is provided AS IS without warranty of any kind.

## User flow
During Sign-up or password reset user journey, the policy calls to a Rest API. Sign-up **LocalAccountSignUpWithLogonEmailFirstStep** technical profile and password reset **LocalAccountDiscoveryUsingEmailAddress** technical profiles invokes **REST-API-SendVerificationEmail** verification technical profile who sends the verification email. B2C sends the user email address and policy name as input claims. The REST API endpoint generates a URL with the policy name and send it to the email address.

End user needs to open the email message and click on the 'Confirm account', which redirects the user back to the same Azure AD B2C policy. The user journey checks if user comes back after email verification, and skip the first skip this step.

## Sending Application Data

**Important**: The way Azure AD B2C accepts the client assertion probably will be changed in the future. Until this change, you can use the client assertion method safely, but you should prepare yourself to the changes.

The key of sending data to Azure AD B2C custom policy is to package the data into a JWT token as claims (client assertion). In this case, we send the user's email address to Azure B2C. Sending JWT token requires adding two query strings in the request to the policy.
1.	**client_assertion_type** The value always should be `urn:ietf:params:oauth:client-assertion-type:jwt-bearer`, which is a constant string.
2.	**client_assertion** The value is a JWT token containing input claims for the policy and signed by your application.

### Packaging Data to Send
As described earlier, the data to be sent, needs to be packaged as JWT with claims. In this example, we add the  _ValidationEmail_ claim, which represents the user email address. You can add more claims as nessuccry. The app generates a JWT representation, which can then be used as a client assertion. 

### Signing the client assertion
With client assertion, the client signs the JWT token to prove the token request comes from your web application, by using signing key. You need the signing key, later to store it B2C keys. Your policy uses that key to validate the incoming JWT token, issued by your web application. Use following PowerShell code to generate client secret.

```PowerShell
$bytes = New-Object Byte[] 32
$rand = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rand.GetBytes($bytes)
$rand.Dispose()
$newClientSecret = [System.Convert]::ToBase64String($bytes)
$newClientSecret
```

> Note: the PowerShell generates a secret string. But you can define and use any arbitrary string.


###  Add the sign-in key to Azure AD B2C
As mentioned, Azure AD B2C needs the client secret to validate the incoming JWT token. You need to store the client secret your application uses to sign in, in your Azure AD B2C tenant:  

1.  Go to your Azure AD B2C tenant, and select **B2C Settings** > **Identity Experience Framework**
2.  Select **Policy Keys** to view the keys available in your tenant.
3.  Click **+Add**.
4.  For **Options**, use **Manual**.
5.  For **Name**, use `ClientAssertionSigningKey`.  
    The prefix `B2C_1A_` might be added automatically.
6.  In the **Secret** box, enter your sign-in key you generated earlier
7.  For **Key usage**, use **Encryption**.
8.  Click **Create**
9.  Confirm that you've created the key `B2C_1A_ClientAssertionSigningKey`.

### How Azure AD B2C custom policy read the client assertion?
The Relying Party is responsible to read the input claim (client assertion). Make sure you specify the `client_secret` with the policy key you already created. In this example, we read the input claim `ValidationEmail` to both claim types: _email_ this claim use to store the email address in the user account. And the second one is _ReadOnlyEmail_ for display only

```XML
<CryptographicKeys>
    <Key Id="client_secret" StorageReferenceId="B2C_1A_ClientAssertionSigningKey" />
</CryptographicKeys>
<InputClaims>
    <InputClaim ClaimTypeReferenceId="email" PartnerClaimType="ValidationEmail" />
    <InputClaim ClaimTypeReferenceId="ReadOnlyEmail" PartnerClaimType="ValidationEmail" />
</InputClaims>
```
 
## Application Settings
To test the sample solution, open the `AADB2C.Invite.sln` Visual Studio solution in Visual Studio. In the `AADB2C.Invite` project, open the `appsettings.json`. Replace the app settings with your own values:
* **SMTPServer**: Your SMTP server
* **SMTPPort**: Your SMTP server port number
* **SMTPUsername**: SMTP user name, if necessary
* **SMTPPassword**: SMTP password, if necessary
* **SMTPUseSSL**: SMTP use SSL, true of false
* **SMTPFromAddress**: Send from email address
* **SMTPSubject**: The invitation email's subject
* **ClientSigningKey**: The JTW signature secret you generated earlier with the PowerShell.
* **SignUpUrl**: Full url to your policy. Replace your policy name with `{0}`. The policy name is sent to the reset API.


For example:

```JSON
  "AppSettings": {
    "SMTPServer": "smtp.sendgrid.net",
    "SMTPPort": 587,
    "SMTPUsername": "sendgrid-service@contoso.com",
    "SMTPPassword": "1234",
    "SMTPUseSSL": true,
    "SMTPFromAddress": "admin@contoso.com",
    "SMTPSubject": "Sign-up account email verification",
    "ClientSigningKey": "VK62QTn0m1hMcn0DQ3RPYDAr6yIiSvYgdRwjZtU5QhI=",
    "SignUpUrl": "https://login.microsoftonline.com/contoso.onmicrosoft.com/oauth2/v2.0/authorize?p={0}&client_id=0239a9cc-309c-4d41-87f1-31288feb2e82&nonce=defaultNonce&redirect_uri=https%3A%2F%2Fjwt.ms&scope=openid&response_type=id_token&prompt=login"
  }
```
 