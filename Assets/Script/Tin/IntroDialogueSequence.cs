using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "IntroOpeningDialogue", menuName = "Story/Intro Dialogue Sequence")]
public sealed class IntroDialogueSequence : ScriptableObject
{
    [TextArea(2, 5)]
    public List<string> lines = new List<string>();
}