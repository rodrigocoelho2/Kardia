using Kardia.Library.Enums;

namespace Kardia.Website.Model;

/// <summary>
/// The AI card of a screen: hidden until the server says an AI provider is set up, then a button that writes a short
/// report about that screen (and that day).
/// </summary>
/// <param name="Topic">What the report looks at.</param>
/// <param name="Date">The day shown (null: today).</param>
/// <param name="Prompt">The button's words ("Explain my recovery").</param>
/// <param name="Visible">Shown from the start (the page already knows a provider is set up).</param>
/// <param name="Auto">Writes the report by itself when there's none yet (Today).</param>
public sealed record AiCardModel(AiCoachTopic Topic, DateOnly? Date, string Prompt, bool Visible = false, bool Auto = false);
