using System;
using System.Security.Cryptography;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Business.Abstract;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;

namespace Business.Concrete;

public sealed class EmailVerificationManager(
    IUserDal userDal,
    IEmailVerificationChallengeDal challengeDal,
    IEmailVerificationSender sender,
    IUnitOfWork unitOfWork) : IEmailVerificationService
{
    public async Task<IResult> RequestAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await userDal.GetAsync(item => item.Id == userId && item.Status, cancellationToken);
        if (user is null) return new NotFoundResult("Active account was not found.");
        if (user.EmailVerifiedAt.HasValue) return new ConflictResult("Email is already verified.");

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var challenge = await challengeDal.GetAsync(item => item.UserId == userId, cancellationToken);
        if (challenge is null)
        {
            challenge = new EmailVerificationChallenge { UserId = userId };
            await challengeDal.AddAsync(challenge, cancellationToken);
        }
        else challengeDal.Update(challenge);
        challenge.TokenHash = Convert.ToHexString(SHA256.HashData(Convert.FromHexString(token)));
        challenge.CreatedAt = now;
        challenge.ExpiresAt = now.AddMinutes(30);
        challenge.UsedAt = null;
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("Email verification request changed concurrently. Try again.");
        }
        try
        {
            await sender.SendAsync(user.Email, token, cancellationToken);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException)
        {
            await challengeDal.ExpireIfMatchingAsync(userId, challenge.TokenHash, DateTime.UtcNow,
                cancellationToken);
            return new ServiceUnavailableResult("Verification email could not be delivered. Try again later.");
        }
        return new SuccessResult("Email verification code sent.");
    }

    public async Task<IResult> ConfirmAsync(int userId, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || !token.All(Uri.IsHexDigit))
            return new ErrorResult("Email verification code is invalid.");
        var user = await userDal.GetAsync(item => item.Id == userId && item.Status, cancellationToken);
        if (user is null) return new NotFoundResult("Active account was not found.");
        if (user.EmailVerifiedAt.HasValue) return new ConflictResult("Email is already verified.");
        var challenge = await challengeDal.GetAsync(item => item.UserId == userId, cancellationToken);
        if (challenge is null || challenge.UsedAt.HasValue || challenge.ExpiresAt <= DateTime.UtcNow)
            return new ErrorResult("Email verification code is invalid or expired.");

        var submittedHash = SHA256.HashData(Convert.FromHexString(token));
        if (!CryptographicOperations.FixedTimeEquals(submittedHash, Convert.FromHexString(challenge.TokenHash)))
            return new ErrorResult("Email verification code is invalid or expired.");
        user.EmailVerifiedAt = DateTime.UtcNow;
        challenge.UsedAt = user.EmailVerifiedAt;
        userDal.Update(user);
        challengeDal.Update(challenge);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("Email verification code was already used or replaced.");
        }
        return new SuccessResult("Email address verified.");
    }
}
