// NOTA: clase estática de editor, no es un componente.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VDF
{
    /// <summary>
    /// Monta la iluminación de la escena: luz principal, relleno, sonda de reflexión y
    /// post-proceso.
    ///
    /// Es **la mayor mejora visual por el menor esfuerzo**, y no necesita ni una sola imagen.
    /// El motivo de que una escena sin tocar parezca "de videojuego de hace veinte años" no
    /// suele ser la falta de texturas: es que todo recibe exactamente la misma luz plana, sin
    /// oclusión en los recovecos ni un reflejo que insinúe de qué material está hecho.
    ///
    /// Un tablero metálico sin **sonda de reflexión** no tiene nada que reflejar, así que su
    /// metalicidad no se ve: los bornes niquelados salen gris mate. Esa sonda es la pieza
    /// que más se nota de todo este bloque.
    /// </summary>
    public static class VdfIluminacion
    {
        public const string NombreRaiz = "VDF_Iluminacion";

        /// <summary>
        /// Deja la escena iluminada como un laboratorio: una luz cenital dominante, un
        /// relleno frío por el lado opuesto y un rebote cálido desde abajo.
        /// </summary>
        public static string Montar(Transform centro = null)
        {
            var sb = new System.Text.StringBuilder();

            var previa = GameObject.Find(NombreRaiz);
            if (previa != null) Object.DestroyImmediate(previa);

            var raiz = new GameObject(NombreRaiz);
            Vector3 foco = centro != null ? centro.position : new Vector3(0f, 1.2f, 0f);

            // --- Luz principal: el sol que entra por la ventana ------------------------
            // Desde la fase 2 el módulo está dentro de un aula cerrada, así que una
            // direccional genérica deja de tener sentido físico: el techo la tapa y lo que
            // queda son lamidos de luz por las juntas. Ahora apunta como entraría por la
            // ventana de la pared izquierda (-X), de modo que su dirección y las sombras
            // coinciden con lo que el usuario ve en la pared.
            var sol = BuscarOCrearDireccional();
            sol.transform.rotation = Quaternion.Euler(34f, 68f, 0f);
            sol.color = new Color(1f, 0.97f, 0.90f);
            sol.intensity = 1.05f;
            sol.shadows = LightShadows.Soft;
            sol.shadowStrength = 0.6f;
            sb.AppendLine("sol por la ventana: direccional 34/68, intensidad 1.05, sombra suave");

            // --- Relleno frío ----------------------------------------------------------
            var relleno = new GameObject("Relleno").AddComponent<Light>();
            relleno.transform.SetParent(raiz.transform, false);
            relleno.type = LightType.Directional;
            relleno.transform.rotation = Quaternion.Euler(22f, -115f, 0f);
            relleno.color = new Color(0.74f, 0.81f, 1f);
            relleno.intensity = 0.42f;
            relleno.shadows = LightShadows.None;
            sb.AppendLine("relleno frio por el lado opuesto, sin sombras");

            // --- Fluorescentes del techo -----------------------------------------------
            // OJO: en URP las luces de tipo Rectangle (area) son SOLO HORNEADAS. La versión
            // anterior creaba una y, sin horneado, no aportaba un solo lumen: parecía que
            // iluminaba porque el sol hacía todo el trabajo. Aquí se usan focos, que URP sí
            // evalúa en tiempo real, colocados bajo las luminarias que construye
            // VdfEntornoBuilder para que la luz salga de donde se ve la caja encendida.
            int fluorescentes = MontarFluorescentes(raiz.transform, foco);
            sb.AppendLine("fluorescentes de techo: " + fluorescentes + " focos en tiempo real");

            // --- Foco sobre la cara del tablero ----------------------------------------
            // Por qué hace falta uno dedicado: el tablero mira a -Z y ni el sol (que entra
            // por -X) ni los fluorescentes (que bajan a plomo) le dan de frente, así que la
            // cara con los bornes y las etiquetas se quedaba en penumbra. Se notó al medir
            // el brillo medio de dos entregas: 143,6 en la que tenía horneado contra 64,7
            // en una reconstruida, y la diferencia era justo esto. Es luz de museo: ilumina
            // lo que hay que leer, sin sombras, y por eso no cuesta un paso de render.
            var frente = new GameObject("Luz_FrenteTablero").AddComponent<Light>();
            frente.transform.SetParent(raiz.transform, false);
            frente.type = LightType.Spot;
            // Apunta al centro del tablero en vez de llevar un ángulo fijo: con el foco abierto
            // a 95° el cono se comía medio banco y la encimera salía quemada de blanco en las
            // capturas. Cerrado y apuntado, ilumina la cara y deja la mesa en su tono.
            Vector3 sitio = foco + new Vector3(0f, 0.45f, -0.85f);
            frente.transform.position = sitio;
            frente.transform.rotation = Quaternion.LookRotation((foco - sitio).normalized, Vector3.up);
            frente.spotAngle = 55f;
            frente.innerSpotAngle = 30f;
            frente.color = new Color(1f, 0.99f, 0.96f);
            frente.intensity = 1.1f;
            frente.range = 3.5f;
            frente.shadows = LightShadows.None;
            sb.AppendLine("foco frontal sobre la cara del tablero, intensidad 0.95 sin sombras");

            // --- Ambiente --------------------------------------------------------------
            // Un aula cerrada no recibe cielo, así que el ambiente sube un poco y se vuelve
            // más neutro: hace de rebote de las paredes, que es lo que falta sin horneado.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.55f, 0.60f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.42f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.25f, 0.23f);
            RenderSettings.ambientIntensity = 1f;
            sb.AppendLine("ambiente de tres bandas, subido para interior cerrado");

            // --- Sonda de reflexión ----------------------------------------------------
            // Sin esto, el metal no tiene nada que reflejar y se ve gris mate.
            var sondaGo = new GameObject("SondaReflexion");
            sondaGo.transform.SetParent(raiz.transform, false);
            sondaGo.transform.position = foco;

            var sonda = sondaGo.AddComponent<ReflectionProbe>();
            sonda.mode = ReflectionProbeMode.Realtime;
            sonda.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            sonda.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            sonda.size = new Vector3(6f, 4f, 6f);
            sonda.resolution = 256;
            sonda.hdr = true;
            sonda.shadowDistance = 10f;
            sonda.cullingMask = ~0;
            sonda.RenderProbe();
            sb.AppendLine("sonda de reflexion 256 HDR renderizada");

            return sb.ToString();
        }

        /// <summary>
        /// Coloca un foco bajo cada luminaria del aula. Si todavía no hay aula construida,
        /// deja un solo foco sobre el tablero, para que la escena siga iluminada.
        ///
        /// Se buscan los objetos "Luminaria_*" en vez de recalcular sus coordenadas: así la
        /// luz y la caja que se ve encendida no pueden separarse al cambiar las medidas del
        /// aula en <see cref="VdfEntornoConfig"/>.
        /// </summary>
        private static int MontarFluorescentes(Transform padre, Vector3 foco)
        {
            var posiciones = new System.Collections.Generic.List<Vector3>();

            var entorno = GameObject.Find(VdfEntornoBuilder.NombreRaiz);
            if (entorno != null)
            {
                var hijos = entorno.GetComponentsInChildren<Transform>();
                for (int i = 0; i < hijos.Length; i++)
                    if (hijos[i].name.StartsWith("Luminaria_"))
                        posiciones.Add(hijos[i].position);
            }

            if (posiciones.Count == 0)
                posiciones.Add(foco + new Vector3(0f, 1.15f, -0.35f));

            for (int i = 0; i < posiciones.Count; i++)
            {
                var luz = new GameObject("Fluorescente_" + (i + 1)).AddComponent<Light>();
                luz.transform.SetParent(padre, false);
                luz.type = LightType.Spot;
                luz.transform.position = posiciones[i] - new Vector3(0f, 0.05f, 0f);
                luz.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                luz.spotAngle = 120f;
                luz.innerSpotAngle = 70f;
                luz.color = new Color(0.96f, 0.98f, 1f);
                luz.intensity = 4.2f;
                luz.range = 7f;
                // Sin sombras a propósito: son luces de relleno y cada foco con sombras
                // cuesta un paso de render extra, que en Quest no sale a cuenta.
                luz.shadows = LightShadows.None;
            }

            return posiciones.Count;
        }

        private static Light BuscarOCrearDireccional()
        {
            var luces = Object.FindObjectsByType<Light>();
            for (int i = 0; i < luces.Length; i++)
                if (luces[i].type == LightType.Directional && luces[i].transform.parent == null)
                    return luces[i];

            var go = new GameObject("Directional Light");
            return go.AddComponent<Light>();
        }

        /// <summary>
        /// Volumen global de post-proceso: tonemapping, algo de bloom y viñeta.
        ///
        /// El tonemapping es lo importante. Sin él, URP manda el color lineal a pantalla tal
        /// cual y los brillos se queman a blanco plano. Con ACES, los reflejos conservan
        /// matiz y la imagen deja de parecer una captura de editor.
        /// </summary>
        public static string MontarPostProceso()
        {
            var sb = new System.Text.StringBuilder();

            var previo = GameObject.Find("VDF_PostProceso");
            if (previo != null) Object.DestroyImmediate(previo);

            var go = new GameObject("VDF_PostProceso");
            var volumen = go.AddComponent<Volume>();
            volumen.isGlobal = true;
            volumen.priority = 10f;

            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            const string ruta = "Assets/_VDF/Data/VDF_PostProceso.asset";
            AssetDatabase.CreateAsset(perfil, ruta);

            var tone = perfil.Add<Tonemapping>(true);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;
            sb.AppendLine("tonemapping ACES");

            var bloom = perfil.Add<Bloom>(true);
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.22f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.62f;
            sb.AppendLine("bloom suave (solo pilotos y reflejos fuertes)");

            var color = perfil.Add<ColorAdjustments>(true);
            color.postExposure.overrideState = true;
            color.postExposure.value = 0.15f;
            color.contrast.overrideState = true;
            color.contrast.value = 8f;
            color.saturation.overrideState = true;
            color.saturation.value = -4f;
            sb.AppendLine("ajuste de color: algo de contraste, menos saturacion");

            var vineta = perfil.Add<Vignette>(true);
            vineta.intensity.overrideState = true;
            vineta.intensity.value = 0.22f;
            vineta.smoothness.overrideState = true;
            vineta.smoothness.value = 0.6f;
            sb.AppendLine("vineta ligera");

            volumen.sharedProfile = perfil;
            EditorUtility.SetDirty(perfil);
            AssetDatabase.SaveAssets();

            return sb.ToString();
        }
    }
}
#endif
