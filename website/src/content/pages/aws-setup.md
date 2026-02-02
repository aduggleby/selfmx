---
title: AWS SES Setup Guide
description: Complete guide to setting up AWS Simple Email Service for use with SelfMX.
toc: true
---

## Overview

SelfMX uses Amazon Simple Email Service (SES) to deliver emails. This guide walks you through creating an AWS account, configuring IAM credentials, and setting up SES for production use.

## Prerequisites

- An AWS account (create one at [aws.amazon.com](https://aws.amazon.com))
- A domain you control (for email sending)
- Access to your domain's DNS settings

## Quick Start (CLI)

If you're comfortable with the AWS CLI, here's the fast path:

```bash
# Install AWS CLI (if not already installed)
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip && sudo ./aws/install

# Configure AWS CLI with your root/admin credentials temporarily
aws configure

# Create IAM user for SelfMX
aws iam create-user --user-name selfmx-ses

# Create and attach the SES policy (uses SES v2 API)
cat > /tmp/selfmx-ses-policy.json << 'EOF'
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "SelfMXSESSending",
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail"
      ],
      "Resource": "*"
    },
    {
      "Sid": "SelfMXSESIdentities",
      "Effect": "Allow",
      "Action": [
        "ses:CreateEmailIdentity",
        "ses:GetEmailIdentity",
        "ses:DeleteEmailIdentity",
        "ses:GetAccount"
      ],
      "Resource": "*"
    }
  ]
}
EOF

aws iam put-user-policy \
  --user-name selfmx-ses \
  --policy-name SelfMXSESPolicy \
  --policy-document file:///tmp/selfmx-ses-policy.json

# Create access keys (save these securely!)
aws iam create-access-key --user-name selfmx-ses
```

**Note:** Domain verification is handled automatically by SelfMX - you don't need to do it manually in AWS.

## Step-by-Step Guide

### Step 1: Create an AWS Account

1. Go to [aws.amazon.com](https://aws.amazon.com) and click **Create an AWS Account**
2. Follow the signup process (requires email, phone, and payment method)
3. Choose the **Free Tier** - SES includes 62,000 free emails/month when sending from EC2

### Step 2: Create an IAM User for SelfMX

Never use your root AWS credentials. Create a dedicated IAM user with minimal permissions.

#### Using the AWS Console

1. Sign in to the [AWS Console](https://console.aws.amazon.com)
2. Navigate to **IAM** > **Users** > **Create user**
3. Enter username: `selfmx-ses`
4. Click **Next**
5. Select **Attach policies directly**
6. Click **Create policy** and switch to the **JSON** tab
7. Paste this policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "SelfMXSESSending",
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail"
      ],
      "Resource": "*"
    },
    {
      "Sid": "SelfMXSESIdentities",
      "Effect": "Allow",
      "Action": [
        "ses:CreateEmailIdentity",
        "ses:GetEmailIdentity",
        "ses:DeleteEmailIdentity",
        "ses:GetAccount"
      ],
      "Resource": "*"
    }
  ]
}
```

8. Name the policy `SelfMXSESPolicy` and create it
9. Back in the user creation, refresh and select your new policy
10. Complete user creation

#### Create Access Keys

1. Go to **IAM** > **Users** > **selfmx-ses**
2. Click the **Security credentials** tab
3. Under **Access keys**, click **Create access key**
4. Select **Application running outside AWS**
5. Click **Create access key**
6. **Save both the Access Key ID and Secret Access Key** - you won't see the secret again!

### Step 3: Choose Your AWS Region

SES is available in these regions. Choose one close to your server:

| Region | Location | Endpoint |
|--------|----------|----------|
| `us-east-1` | N. Virginia | email.us-east-1.amazonaws.com |
| `us-east-2` | Ohio | email.us-east-2.amazonaws.com |
| `us-west-2` | Oregon | email.us-west-2.amazonaws.com |
| `eu-west-1` | Ireland | email.eu-west-1.amazonaws.com |
| `eu-central-1` | Frankfurt | email.eu-central-1.amazonaws.com |
| `ap-south-1` | Mumbai | email.ap-south-1.amazonaws.com |
| `ap-southeast-2` | Sydney | email.ap-southeast-2.amazonaws.com |

**Note:** You must configure SES in the region you choose. SES settings don't replicate across regions.

### Step 4: Request Production Access

New SES accounts start in **sandbox mode** with restrictions:
- Can only send to verified email addresses
- Maximum 200 emails per 24 hours
- Maximum 1 email per second

To send to any recipient, you must request production access.

#### Prerequisites for Production Access

Before requesting, ensure you have:

1. **A verified domain** with DKIM configured
2. **SPF record** for your domain:
   ```
   v=spf1 include:amazonses.com ~all
   ```
3. **DMARC record** (required since 2024):
   ```
   _dmarc.yourdomain.com TXT "v=DMARC1; p=none; rua=mailto:dmarc@yourdomain.com"
   ```
4. **A working website** on your domain showing your business

#### Request Production Access

1. In the SES Console, go to **Account dashboard**
2. Click **Request production access**
3. Select **Transactional** as your mail type
4. For **Website URL**, enter your domain's website
5. In the **Use case description**, be detailed. Example:

> We operate a SaaS application that requires transactional email for:
> - User registration and email verification
> - Password reset notifications
> - Order confirmations and receipts
> - Account activity alerts
>
> We implement the following best practices:
> - Double opt-in for all marketing communications
> - Automatic bounce and complaint handling via SNS
> - Unsubscribe links in all emails
> - Email list hygiene with regular cleanup
>
> Expected volume: 1,000-5,000 emails per day
> We commit to maintaining bounce rates below 5% and complaint rates below 0.1%.

6. Click **Submit request**

AWS typically responds within 24 hours. If denied, you can resubmit with more detail.

### Step 5: Configure SelfMX

Once you have your credentials and domain verified, configure SelfMX:

```bash
# For the installer
export AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
export AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
export AWS_REGION="us-east-1"
```

Or in your environment variables / docker-compose:

```yaml
environment:
  - Aws__Region=us-east-1
  - Aws__AccessKeyId=AKIAIOSFODNN7EXAMPLE
  - Aws__SecretAccessKey=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
```

## IAM Policy Reference

### Minimal Policy (Send Only)

For maximum security, use this minimal policy that only allows sending:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail"
      ],
      "Resource": "*"
    }
  ]
}
```

### Full Policy (With Domain Management)

This policy allows SelfMX to manage domain verification (uses SES v2 API):

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "SESSending",
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail"
      ],
      "Resource": "*"
    },
    {
      "Sid": "SESIdentityManagement",
      "Effect": "Allow",
      "Action": [
        "ses:CreateEmailIdentity",
        "ses:GetEmailIdentity",
        "ses:DeleteEmailIdentity",
        "ses:GetAccount"
      ],
      "Resource": "*"
    }
  ]
}
```

### Restricted to Specific Domain

To restrict sending to a specific domain only:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ses:SendEmail",
        "ses:SendRawEmail"
      ],
      "Resource": [
        "arn:aws:ses:us-east-1:123456789012:identity/yourdomain.com",
        "arn:aws:ses:us-east-1:123456789012:identity/*@yourdomain.com"
      ]
    }
  ]
}
```

Replace `123456789012` with your AWS account ID and `yourdomain.com` with your domain.

## Troubleshooting

### "User is not authorized to perform ses:SendEmail"

Your IAM user doesn't have the correct policy attached. Verify the policy includes `ses:SendEmail` and `ses:SendRawEmail`.

### "User is not authorized to perform ses:GetAccount" or "ses:CreateEmailIdentity"

SelfMX uses the **SES v2 API**, which has different IAM action names than the v1 API. If you're seeing this error, your IAM policy likely uses the old v1 API actions.

**V1 API actions (outdated):**
- `ses:VerifyDomainIdentity`, `ses:VerifyDomainDkim`
- `ses:GetIdentityVerificationAttributes`, `ses:GetIdentityDkimAttributes`
- `ses:DeleteIdentity`, `ses:ListIdentities`

**V2 API actions (required by SelfMX):**
- `ses:CreateEmailIdentity` (replaces VerifyDomainIdentity)
- `ses:GetEmailIdentity` (replaces GetIdentityVerificationAttributes/DkimAttributes)
- `ses:DeleteEmailIdentity` (replaces DeleteIdentity)
- `ses:GetAccount` (for checking SES account status)

Update your IAM policy to use the v2 actions as shown in the policies above.

### Domain Verification Pending

- DNS propagation can take up to 72 hours
- Verify records with: `dig TXT _amazonses.yourdomain.com`
- Check for typos in DNS record values

### Stuck in Sandbox

Ensure you have:
- A verified domain with DKIM
- SPF and DMARC records configured
- A detailed use case description in your production request
- A working website on your domain

### Emails Going to Spam

1. Verify DKIM is working: check email headers for `dkim=pass`
2. Add SPF record: `v=spf1 include:amazonses.com ~all`
3. Add DMARC record: `v=DMARC1; p=none; rua=mailto:dmarc@yourdomain.com`
4. Warm up your sending reputation gradually

### Rate Limit Errors

- Sandbox: 1 email/second, 200 emails/day
- Production: 14 emails/second, 50,000 emails/day (initial)
- Request limit increase via AWS Support if needed

## Cost Estimate

| Usage | Monthly Cost |
|-------|--------------|
| First 62,000 emails (from EC2) | Free |
| Additional emails | $0.10 per 1,000 |
| Attachments | $0.12 per GB |

Most small-to-medium applications stay within the free tier.

## Security Best Practices

1. **Never use root credentials** - Always create an IAM user
2. **Use minimal permissions** - Only grant what's needed
3. **Rotate access keys** - Change keys every 90 days
4. **Monitor usage** - Set up CloudWatch alarms for unusual activity
5. **Enable MFA** - Protect your AWS account with multi-factor authentication

## Next Steps

- [Install SelfMX](/install) - Complete installation guide
- [API Reference](/api) - Start sending emails
- [Cloudflare DNS Setup](/install#cloudflare-optional) - Automatic DNS management

## References

- [AWS SES Developer Guide](https://docs.aws.amazon.com/ses/latest/dg/)
- [IAM Permissions for SES](https://docs.aws.amazon.com/ses/latest/dg/control-user-access.html)
- [Request Production Access](https://docs.aws.amazon.com/ses/latest/dg/request-production-access.html)
- [Easy DKIM Setup](https://docs.aws.amazon.com/ses/latest/dg/send-email-authentication-dkim-easy.html)
