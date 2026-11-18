/// <summary>
/// The 9 elements from the design doc (section 5).
/// Cycle: Fire > Tree > Rock > Lightning > Water > Fire. Dark and Light beat each other.
/// Non-Neutral beats everything by half (+12.5%) except Neutral; Neutral beats only Non-Neutral.
/// Integer values are stored in assets and saves: only append new values.
/// </summary>
public enum CardElement
{
    Fire = 0,
    Water = 1,
    Tree = 2,
    Lightning = 3,
    Light = 4,
    Dark = 5,
    Rock = 6,
    Neutral = 7,
    NonNeutral = 8
}
