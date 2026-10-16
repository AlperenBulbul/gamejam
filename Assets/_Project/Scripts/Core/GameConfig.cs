using System;
using UnityEngine;

namespace LoadBearing
{
    [Serializable]
    public struct FloorLimitRule
    {
        public int minArea;
        public int maxArea;
        public int maxFloors;

        public FloorLimitRule(int min, int max, int floors) { minArea = min; maxArea = max; maxFloors = floors; }
    }

    /// Oyundaki TÜM sayılar burada. Kodda sabit sayı yazılmaz; buradan okunur.
    [CreateAssetMenu(menuName = "Load Bearing/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        /// GameManager Awake'te doldurur. Her yerden GameConfig.Current ile okunur.
        public static GameConfig Current { get; set; }

        [Header("Ekonomi (TL)")]
        public int startBalance = 500;
        public int feePerFloor = 400;
        public int siteWorkersPerDay = 250;
        public int materialPerStep = 150;
        public int officePerDay = 300;
        public int fixCost = 20;

        [Header("İnşaat")]
        public float stepSeconds = 40f;
        public float wrongClickPenalty = 5f;
        [Tooltip("Hataların adım içinde başlayabileceği saniye aralığı")]
        public Vector2 faultWindow = new Vector2(5f, 30f);
        public int defectCollapse = 3;
        [Tooltip("İndeks = adım (0 = temel, 1 = 1. kat ...)")]
        public int[] workersPerStep = { 3, 4, 4, 5, 5, 6, 6 };
        public int[] faultsPerStep = { 1, 1, 1, 2, 2, 2, 2 };

        [Header("Kurallar")]
        public int penaltyLimit = 3;
        public int wrongfulRejectLimit = 3;
        public FloorLimitRule[] floorLimits =
        {
            new FloorLimitRule(0, 199, 2),
            new FloorLimitRule(200, 399, 4),
            new FloorLimitRule(400, 99999, 6),
        };
        public string[] registry = { "İMO-1047", "İMO-2213", "İMO-3390", "İMO-4521", "İMO-5876", "İMO-6102" };

        [Header("Müşteri sabrı")]
        [Tooltip("Sabır bu eşiklerin üstündeyse o aşamada: Sakin > 0.70, Sabırsız > 0.40, Sinirli > 0.15, Patlama > 0")]
        public float[] patienceStages = { 0.70f, 0.40f, 0.15f, 0.00f };
        public float nextCustomerDelay = 3f;
        [Tooltip("Müşterinin kapıdan gişeye yürüme süresi (sn)")]
        public float customerWalkSeconds = 2.5f;

        [Header("Konuşma")]
        public float speechCps = 30f;
        public int blipEveryNChars = 2;
        public float bubbleHold = 2.5f;

        [Header("Kamera")]
        public float lookYaw = 60f;
        [Tooltip("Yukarı bakış sınırı (derece)")]
        public float lookPitch = 25f;
        [Tooltip("Aşağı bakış sınırı (derece) — mühürler ve masanın ön kenarı için daha geniş")]
        public float lookPitchDown = 45f;
        [Range(0.05f, 0.45f)] public float lookEdge = 0.2f;
        public float focusTime = 0.35f;

        public int FloorLimitFor(int areaM2)
        {
            foreach (var r in floorLimits)
                if (areaM2 >= r.minArea && areaM2 <= r.maxArea) return r.maxFloors;
            return 0;
        }

        public int WorkersForStep(int step) => workersPerStep[Mathf.Clamp(step, 0, workersPerStep.Length - 1)];
        public int FaultsForStep(int step) => faultsPerStep[Mathf.Clamp(step, 0, faultsPerStep.Length - 1)];

        public PatienceStage StageFor(float patience01)
        {
            for (int i = 0; i < patienceStages.Length; i++)
                if (patience01 > patienceStages[i]) return (PatienceStage)i;
            return PatienceStage.Gone;
        }
    }
}
