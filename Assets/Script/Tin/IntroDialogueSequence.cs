using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IntroOpeningDialogue", menuName = "Story/Intro Dialogue Sequence")]
public sealed class IntroDialogueSequence : ScriptableObject
{
    [TextArea(2, 5)]
    public List<string> lines = new List<string>();

    [TextArea(2, 5)]
    public List<string> englishLines = new List<string>();

    public IReadOnlyList<string> LocalizedLines => GameLocalization.IsVietnamese || englishLines.Count == 0
        ? lines
        : englishLines;
}
