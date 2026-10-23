using System.Numerics;

namespace D4Assist.Navigation;

/// <summary>
/// A traversal the surface walk can't make on foot (a jump, a duck-under, a slide): the player uses it at one end to land at
/// the other. World units, each end at its floor's height; a one-way link is used only from <see cref="From"/> (a slide down).
/// </summary>
public readonly record struct SurfaceLink(Vector3 From, Vector3 To, bool OneWay = false);
