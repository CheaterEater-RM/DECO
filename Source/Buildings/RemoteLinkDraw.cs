using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    // Shared rendering for the remote-button <-> door links. Draws a cyan line from the
    // button (source) to each door (target) with V-chevron arrows pointing toward the door,
    // so the control direction reads at a glance. Modeled on Contagion's trace-graph drawer.
    internal static class RemoteLinkDraw
    {
        private static readonly Color LinkColor = new Color(0f, 1f, 1f, 1f);

        private static Material linkMaterial;

        private static Material LinkMaterial =>
            linkMaterial ??= MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, LinkColor);

        // Draw a directional link from button to door. Always call with the button as the
        // source so both overlays (button-selected and door-selected) agree on direction.
        public static void DrawLink(Vector3 buttonPos, Vector3 doorPos)
        {
            var origin = LiftToOverlay(buttonPos);
            var end = LiftToOverlay(doorPos);
            GenDraw.DrawLineBetween(origin, end, LinkMaterial, 0.08f);
            DrawDirectionArrows(origin, end);
        }

        private static Vector3 LiftToOverlay(Vector3 position)
        {
            position.y = AltitudeLayer.MetaOverlays.AltitudeFor() + 0.1f;
            return position;
        }

        private static void DrawDirectionArrows(Vector3 origin, Vector3 end)
        {
            var direction = end - origin;
            direction.y = 0f;
            var length = direction.magnitude;
            if (length < 0.0001f)
                return;

            direction /= length;

            const float endOffset = 1f;
            if (length < 2.4f * endOffset)
            {
                DrawArrowHead(Vector3.Lerp(origin, end, 0.5f), direction);
                return;
            }

            DrawArrowHead(origin + direction * endOffset, direction);
            DrawArrowHead(end - direction * endOffset, direction);
        }

        private static void DrawArrowHead(Vector3 tip, Vector3 direction)
        {
            const float armLength = 0.15f;
            const float armWidth = 0.11f;
            var perpendicular = new Vector3(-direction.z, 0f, direction.x);
            var back = tip - direction * armLength;
            var leftArm = back + perpendicular * armWidth;
            var rightArm = back - perpendicular * armWidth;
            GenDraw.DrawLineBetween(tip, leftArm, LinkMaterial, 0.06f);
            GenDraw.DrawLineBetween(tip, rightArm, LinkMaterial, 0.06f);
        }
    }
}
