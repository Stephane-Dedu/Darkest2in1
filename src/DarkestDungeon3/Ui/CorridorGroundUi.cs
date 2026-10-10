using UnityEngine;

namespace DarkestDungeon3.Ui;

/// <summary>Turns DD2's vertical road texture into one horizontal floor strip.</summary>
internal static class CorridorGroundUi
{
    public static void Draw(Rect rect, Texture2D texture, bool mirror)
    {
        var matrix = GUI.matrix;
        try
        {
            // Rotate in the virtual canvas BEFORE its window scale/letterbox transform.
            // RotateAroundPivot applies the turn afterward, displacing each strip differently.
            var turn = Matrix4x4.identity;
            turn.m00 = turn.m11 = 0;
            turn.m01 = -1;
            turn.m10 = 1;
            turn.m03 = rect.center.x + rect.center.y;
            turn.m13 = rect.center.y - rect.center.x;
            GUI.matrix = matrix * turn;
            var turned = new Rect(rect.center.x - rect.height / 2, rect.center.y - rect.width / 2,
                rect.height, rect.width);
            GUI.DrawTextureWithTexCoords(turned, texture, mirror ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1));
        }
        finally { GUI.matrix = matrix; }
    }
}
