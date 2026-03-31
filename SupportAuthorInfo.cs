namespace PassNotes;

internal sealed class SupportAuthorInfo
{
    public static SupportAuthorInfo Current { get; } = new()
    {
        Services =
        [
            new SupportAuthorServiceInfo(
                "pack://application:,,,/Resources/SupportAuthor/Boosty.png",
                [
                    new SupportAuthorValueInfo(
                        "SupportAuthorBoostyProfileAction",
                        "https://boosty.to/minapps",
                        "https://boosty.to/minapps",
                        true,
                        true,
                        false),
                    new SupportAuthorValueInfo(
                        "SupportAuthorBoostySupportAction",
                        "https://boosty.to/minapps/donate",
                        "https://boosty.to/minapps/donate",
                        true,
                        true,
                        false),
                ]),
            new SupportAuthorServiceInfo(
                "pack://application:,,,/Resources/SupportAuthor/GitHub.png",
                [
                    new SupportAuthorValueInfo(
                        "SupportAuthorGitHubProfileAction",
                        "https://github.com/Kirubik",
                        "https://github.com/Kirubik",
                        true,
                        true,
                        false),
                ]),
            new SupportAuthorServiceInfo(
                "pack://application:,,,/Resources/SupportAuthor/YooMoney.png",
                [
                    new SupportAuthorValueInfo(
                        "SupportAuthorYooMoneyFundraiserAction",
                        "https://yoomoney.ru/fundraise/1GNJ8EU7H2S.260325",
                        "https://yoomoney.ru/fundraise/1GNJ8EU7H2S.260325",
                        true,
                        true,
                        false),
                    new SupportAuthorValueInfo(
                        "SupportAuthorYooMoneyTransferAction",
                        "https://yoomoney.ru/to/4100119494062901",
                        "https://yoomoney.ru/to/4100119494062901",
                        true,
                        true,
                        false),
                ]),
        ],
        Contacts =
        [
            new SupportAuthorContactInfo(
                "pack://application:,,,/Resources/SupportAuthor/Telegram.png",
                null,
                [
                    new SupportAuthorValueInfo(
                        "https://t.me/minapps_official",
                        "https://t.me/minapps_official",
                        "https://t.me/minapps_official",
                        false,
                        true,
                        false),
                ]),
            new SupportAuthorContactInfo(
                null,
                "IconData.Email",
                [
                    new SupportAuthorValueInfo(
                        "bizzneskm@gmail.com",
                        "bizzneskm@gmail.com",
                        "mailto:bizzneskm@gmail.com",
                        false,
                        true,
                        false),
                ]),
        ],
    };

    public IReadOnlyList<SupportAuthorServiceInfo> Services { get; init; } = Array.Empty<SupportAuthorServiceInfo>();

    public IReadOnlyList<SupportAuthorContactInfo> Contacts { get; init; } = Array.Empty<SupportAuthorContactInfo>();
}

internal sealed record SupportAuthorServiceInfo(
    string LogoUri,
    IReadOnlyList<SupportAuthorValueInfo> ValueItems);

internal sealed record SupportAuthorContactInfo(
    string? LogoUri,
    string? VectorIconKey,
    IReadOnlyList<SupportAuthorValueInfo> ValueItems);

internal sealed record SupportAuthorValueInfo(
    string TextOrResourceKey,
    string CopyText,
    string ValueUrl,
    bool UseTextResourceKey,
    bool CanCopyValue,
    bool CanOpenValueInBrowser);
