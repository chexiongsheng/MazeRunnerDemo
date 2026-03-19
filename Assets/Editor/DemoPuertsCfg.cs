using System;
using System.Collections.Generic;
using Puerts;

/// <summary>
/// PuerTS configuration for demo-specific C# types (outside the PuertsAgent package).
/// These types are in Assembly-CSharp and need their own [Configure] class.
/// Run "Tools > PuerTS > Generate index.d.ts" in Unity Editor to regenerate.
/// </summary>
[Configure]
public class DemoPuertsCfg
{
    [Typing]
    static IEnumerable<Type> Typings
    {
        get
        {
            return new List<Type>()
            {
                typeof(LLMAgent.MazePlayerBridge),
            };
        }
    }
}
