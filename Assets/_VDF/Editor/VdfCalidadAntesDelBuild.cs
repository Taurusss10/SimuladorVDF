using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VDF.EditorTools
{
    public class VdfCalidadAntesDelBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -1000; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            var objetivo = report.summary.platform;
            string nombre = NombreDePlataforma(objetivo);
            int[] niveles = QualitySettings.GetActiveQualityLevelsForPlatform(nombre);
            if (niveles == null || niveles.Length == 0) return;

            int nivel = niveles[0];
            if (QualitySettings.GetQualityLevel() != nivel)
            {
                Debug.Log("[VDF] Build para " + nombre + ": nivel de calidad «"
                          + QualitySettings.names[nivel] + "» (estaba en «"
                          + QualitySettings.names[QualitySettings.GetQualityLevel()] + "»)");
                QualitySettings.SetQualityLevel(nivel, true);
            }
        }

        private static string NombreDePlataforma(BuildTarget t)
        {
            switch (t)
            {
                case BuildTarget.Android: return "Android";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneLinux64:
                case BuildTarget.StandaloneOSX: return "Standalone";
                default: return t.ToString();
            }
        }
    }
}
