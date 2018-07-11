using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using AADB2C.ActivationLink.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AADB2C.ActivationLink.Controllers
{
    [Route("api/[controller]/[action]")]
    public class IdentityController : Controller
    {
        private readonly AppSettingsModel AppSettings;

        // Demo: Inject an instance of an AppSettingsModel class into the constructor of the consuming class, 
        // and let dependency injection handle the rest
        public IdentityController(IOptions<AppSettingsModel> appSettings)
        {
            this.AppSettings = appSettings.Value;
        }

        [HttpPost(Name = "SendEmail")]
        public async Task<ActionResult> SendEmail()
        {
            string input = null;

            // If not data came in, then return
            if (this.Request.Body == null)
            {
                return StatusCode((int)HttpStatusCode.Conflict, new B2CResponseModel("Request content is null", HttpStatusCode.Conflict));
            }

            // Read the input claims from the request body
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                input = await reader.ReadToEndAsync();
            }

            // Check input content value
            if (string.IsNullOrEmpty(input))
            {
                return StatusCode((int)HttpStatusCode.Conflict, new B2CResponseModel("Request content is empty", HttpStatusCode.Conflict));
            }

            // Convert the input string into InputClaimsModel object
            InputClaimsModel inputClaims = InputClaimsModel.Parse(input);

            if (inputClaims == null)
            {
                return StatusCode((int)HttpStatusCode.Conflict, new B2CResponseModel("Can not deserialize input claims", HttpStatusCode.Conflict));
            }

            if (string.IsNullOrEmpty(inputClaims.email))
            {
                return StatusCode((int)HttpStatusCode.Conflict, new B2CResponseModel("User 'email' is null or empty", HttpStatusCode.Conflict));
            }

            if (string.IsNullOrEmpty(inputClaims.policy))
            {
                return StatusCode((int)HttpStatusCode.Conflict, new B2CResponseModel("Policy name is null or empty", HttpStatusCode.Conflict));
            }
            try
            {
                SendEmail(inputClaims);
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.Conflict, new B2CResponseModel($"General error (REST API): {ex.Message}", HttpStatusCode.Conflict));
            }

            return StatusCode((int)HttpStatusCode.Conflict, new B2CResponseModel($"A verification email sent to you. Please open your mail box and click on the link. If you didn't receive the email, please click on the 'Send verification email' button.", HttpStatusCode.Conflict));
        }

        public void SendEmail(InputClaimsModel inputClaims)
        {

            // Generate link to next step
            string client_assertion = GenerateJWTClientToken(inputClaims.email);
            string link = string.Empty;
            string Body = string.Empty;

            string htmlTemplate = System.IO.File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), "Template.html"));

            link = string.Format(AppSettings.SignUpUrl, inputClaims.policy) +
                $"&client_assertion_type=urn%3Aietf%3Aparams%3Aoauth%3Aclient-assertion-type%3Ajwt-bearer&client_assertion={client_assertion}";

            try
            {
                MailMessage mailMessage = new MailMessage();
                mailMessage.To.Add(inputClaims.email);
                mailMessage.From = new MailAddress(AppSettings.SMTPFromAddress);
                mailMessage.Subject = AppSettings.SMTPSubject;
                mailMessage.Body = string.Format(htmlTemplate, inputClaims.email, link);
                mailMessage.IsBodyHtml = true;
                SmtpClient smtpClient = new SmtpClient(AppSettings.SMTPServer, AppSettings.SMTPPort);
                smtpClient.Credentials = new NetworkCredential(AppSettings.SMTPUsername, AppSettings.SMTPPassword);
                smtpClient.EnableSsl = AppSettings.SMTPUseSSL;
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.Send(mailMessage);

                Console.WriteLine("Email sent");

            }
            catch (Exception ex)
            {
                throw;
            }
        }


        public string GenerateJWTClientToken(string email)
        {
            const string issuer = "http://www.contoso.com";
            const string audience = "http://azure.microsoft.com/B2C/invite";

            // All parameters send to Azure AD B2C needs to be sent as claims
            IList<System.Security.Claims.Claim> claims = new List<System.Security.Claims.Claim>();
            claims.Add(new System.Security.Claims.Claim("verifiedEmail", email, System.Security.Claims.ClaimValueTypes.String, issuer));

            // Use the ida:ClientSigningKey value to sign the token
            // Note: This key phrase needs to be stored also in Azure B2C Keys for token validation
            var securityKey = Encoding.ASCII.GetBytes(AppSettings.ClientSigningKey);

            var signingKey = new SymmetricSecurityKey(securityKey);
            SigningCredentials signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            // Create the signing credetails for JWT
            JwtSecurityToken token = new JwtSecurityToken(
                    issuer,
                    audience,
                    claims,
                    DateTime.Now,
                    DateTime.Now.AddYears(1),
                    signingCredentials);

            // Get the representation of the signed token
            JwtSecurityTokenHandler jwtHandler = new JwtSecurityTokenHandler();
            string jwtOnTheWire = jwtHandler.WriteToken(token);

            return jwtOnTheWire;
        }
    }
}
