using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace VDF.EditorTools
{
    /// <summary>
    /// Deja el proyecto listo para compilar el APK de Quest.
    ///
    /// Por qué existe: al ir a generar el primer instalable para el visor se vio que la
    /// configuración de XR para Android estaba a medias. Existía la entrada de ajustes de
    /// Android, pero **su gestor estaba vacío** (`m_Manager: {fileID: 0}`), o sea sin ningún
    /// loader, y los tres perfiles de mando de Meta Quest estaban desactivados. Con eso el
    /// APK se genera igual y no da ningún error: arranca en una ventana plana, sin
    /// seguimiento de cabeza y sin mandos. Es de los fallos que no se ven hasta que alguien
    /// se pone las gafas.
    ///
    /// Se hace por código y no a mano en la ventana de XR Plug-in Management porque así
    /// queda repetible: si mañana se clona el repositorio en otra máquina, se ejecuta esto y
    /// la configuración es la misma, sin depender de que alguien recuerde qué casillas
    /// marcar.
    /// </summary>
    public static class VdfMenuXrQuest
    {
        [MenuItem("VDF/Preparar el proyecto para Quest")]
        public static void Preparar()
        {
            var resumen = new List<string>();

            // --- 1. El loader de OpenXR para Android --------------------------------------
            XRGeneralSettingsPerBuildTarget porPlataforma;
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,
                                                        out porPlataforma))
            {
                Debug.LogError("[VDF] No hay ajustes de XR en el proyecto. Instala XR Plug-in "
                             + "Management desde el gestor de paquetes.");
                return;
            }

            var ajustes = porPlataforma.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (ajustes == null)
            {
                ajustes = ScriptableObject.CreateInstance<XRGeneralSettings>();
                ajustes.name = "Android Settings";
                AssetDatabase.AddObjectToAsset(ajustes, porPlataforma);
                porPlataforma.SetSettingsForBuildTarget(BuildTargetGroup.Android, ajustes);
                resumen.Add("creados los ajustes de XR para Android");
            }

            if (ajustes.Manager == null)
            {
                var gestor = ScriptableObject.CreateInstance<XRManagerSettings>();
                gestor.name = "Android Providers";
                AssetDatabase.AddObjectToAsset(gestor, porPlataforma);
                ajustes.Manager = gestor;
                resumen.Add("creado el gestor de proveedores, que estaba vacío");
            }

            ajustes.InitManagerOnStart = true;

            bool yaEstaba = false;
            var activos = ajustes.Manager.activeLoaders;
            for (int i = 0; activos != null && i < activos.Count; i++)
                if (activos[i] != null && activos[i].GetType() == typeof(OpenXRLoader)) yaEstaba = true;

            if (!yaEstaba)
            {
                bool ok = XRPackageMetadataStore.AssignLoader(
                    ajustes.Manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
                resumen.Add(ok ? "OpenXR asignado como loader de Android"
                               : "NO SE PUDO asignar el loader de OpenXR");
            }
            else
            {
                resumen.Add("OpenXR ya estaba asignado");
            }

            // --- 2. Los perfiles de mando -------------------------------------------------
            // Se activan los tres a propósito: asi el mismo APK vale para Quest 2 (Oculus
            // Touch), Quest 3 y 3S (Touch Plus) y Quest Pro, sin compilar uno por modelo.
            var openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (openXr == null)
            {
                Debug.LogError("[VDF] No hay ajustes de OpenXR para Android.");
                return;
            }

            // Esta es la que de verdad decide si el APK es una aplicacion de VR o no. Escribe
            // en el AndroidManifest la categoria com.oculus.intent.category.VR y la etiqueta
            // de dispositivos compatibles. Sin ella el APK compila, se instala y arranca, pero
            // el visor lo trata como una aplicacion plana: sale una ventana flotando en el
            // salon, sin seguimiento de cabeza. No da ningun error en consola.
            resumen.Add(Activar<MetaQuestFeature>(openXr, "soporte Meta Quest (manifiesto VR)"));

            resumen.Add(Activar<OculusTouchControllerProfile>(openXr, "Oculus Touch (Quest 2)"));
            resumen.Add(Activar<MetaQuestTouchPlusControllerProfile>(openXr, "Touch Plus (Quest 3 y 3S)"));
            resumen.Add(Activar<MetaQuestTouchProControllerProfile>(openXr, "Touch Pro (Quest Pro)"));

            EditorUtility.SetDirty(porPlataforma);
            EditorUtility.SetDirty(openXr);
            AssetDatabase.SaveAssets();

            Debug.Log("[VDF] Quest: " + string.Join("\n  |  ", resumen.ToArray()));
        }

        private static string Activar<T>(OpenXRSettings ajustes, string nombre)
            where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var rasgo = ajustes.GetFeature<T>();
            if (rasgo == null) return nombre + ": no está en el paquete";
            if (rasgo.enabled) return nombre + ": ya estaba activo";
            rasgo.enabled = true;
            return nombre + ": activado";
        }
    }
}
