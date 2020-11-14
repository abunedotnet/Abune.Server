//-----------------------------------------------------------------------
// <copyright file="OfflineTokenProvider.cs" company="Thomas Stollenwerk (motmot80)">
// Copyright (c) Thomas Stollenwerk (motmot80). All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Abune.Server.Cli.Util
{
    using System;
    using System.IdentityModel.Tokens.Jwt;
    using Abune.Shared.Command;
    using Microsoft.IdentityModel.Tokens;

    /// <summary>Provides OAuth tokens in offline szenarios.</summary>
    public class OfflineTokenProvider
    {
        private readonly SecurityKey signingKey;
        private readonly string issuer;

        /// <summary>Initializes a new instance of the <see cref="OfflineTokenProvider" /> class.</summary>
        /// <param name="issuer">The issuer.</param>
        /// <param name="signingKey">The signing key.</param>
        public OfflineTokenProvider(string issuer, string signingKey)
        {
            this.issuer = issuer;
            this.signingKey = new SymmetricSecurityKey(Convert.FromBase64String(signingKey));
        }

        /// <summary>Creates the JWT token.</summary>
        /// <param name="authenticationChallenge">The authentication challenge.</param>
        /// <param name="expires">The expires.</param>
        /// <returns>Json web token.</returns>
        public string CreateJWTToken(string authenticationChallenge, DateTime expires)
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDesciptor = new SecurityTokenDescriptor();
            tokenDesciptor.Issuer = this.issuer;
            tokenDesciptor.IssuedAt = DateTime.UtcNow;
            tokenDesciptor.Subject = new System.Security.Claims.ClaimsIdentity();
            tokenDesciptor.Subject.AddClaim(new System.Security.Claims.Claim(AuthenticationConstants.JwtClaims.AUTHENTICATIONCHALLENGE, authenticationChallenge));
            tokenDesciptor.Audience = "abune.server";
            tokenDesciptor.SigningCredentials = new SigningCredentials(this.signingKey, "HS256");
            tokenDesciptor.Expires = expires;
            return tokenHandler.CreateEncodedJwt(tokenDesciptor);
        }
    }
}
