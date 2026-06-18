using UnityEngine;
using Verse;

namespace DoorsExpanded
{
    [StaticConstructorOnStartup]
    internal static class RemoteControlTex
    {
        public static readonly Texture2D UseButtonOrLever =
            ContentFinder<Texture2D>.Get("UI/Buttons/UseButtonOrLever");

        public static readonly Texture2D SecuredRemotely =
            ContentFinder<Texture2D>.Get("UI/Buttons/SecuredRemotely");

        public static readonly Texture2D ConnectToButton =
            ContentFinder<Texture2D>.Get("UI/Buttons/ConnectToButton");

        public static readonly Texture2D DisconnectButton =
            ContentFinder<Texture2D>.Get("UI/Buttons/DisconnectButton");

        public static readonly Texture2D Rename =
            ContentFinder<Texture2D>.Get("UI/Buttons/Rename");

        public static readonly Material SecuredOverlayMaterial =
            MaterialPool.MatFrom("UI/Overlays/DE_Secured", ShaderDatabase.Transparent);
    }
}
