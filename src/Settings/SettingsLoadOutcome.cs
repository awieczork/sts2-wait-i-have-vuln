namespace WaitIHaveVuln.Settings;

internal enum SettingsLoadOutcome
{
    Loaded,
    Missing,
    // The unreadable file was moved aside to settings.json.bad.
    Damaged
}
