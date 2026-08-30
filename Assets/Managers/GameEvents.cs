using System;
using Crease.Flying.Player.Health;

// this is a very rudimentary mega class for all game events
// it is very possible that it eventually is inadequate, in which case a more robust system should be implemented
namespace Crease.Events
{
    public enum CoinType
    {
        Regular,
        Dash,
        Health,
    }

    public static class GameEvents
    {

        // Plane flight events
        public static Action<DamageType, float> OnPlaneCollided;
        public static Action<DamageType, float> OnDamageTaken;
        public static Action<DamageType, float> OnDamageHealed;
        public static Action<float> OnSpeedThresholdPassed;
        public static Action OnPlaneDashed;

        // Paper folding events
        public static Action OnFoldPointClicked;
        public static Action OnCreaseAnimationTriggered;

        // In-level events
        public static Action OnStickerCollected;
        public static Action OnStickerSelected;
        public static Action OnStickerRemovedFromPlane;
        public static Action OnDecalsCleared;

        public static Action OnWindFrustumAffected;
        public static Action OnPlaneTrappedInBubble;
        public static Action OnTrappingBubblePopped;
        public static Action OnBubblePopped;
        public static Action OnWaterBucketTipped;

        public static Action OnLevelEndFlagPopped;
        public static Action OnGameplayFinished;
        public static Action OnCheckpointReached;

        public static Action<CoinType> OnCoinCollected;

        public static Action OnSkyTransitionStarted;
        public static Action<bool> OnSkyTransitionComplete;

        // Letter events
        public static Action OnLetterWritingStarted;
        public static Action OnLetterWritingStopped;
        public static Action OnInkCollected;
    }
}
