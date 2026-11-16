// NOTA: clase estática de editor, no es un componente.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Convierte juegos PBR descargados de ambientCG (CC0) en materiales URP/Lit.
    ///
    /// El trabajo real es de formato, no de arte: ambientCG entrega **Roughness y Metalness
    /// como PNG separados**, mientras que URP/Lit espera un único `_MetallicGlossMap` con
    /// metalicidad en R y **suavidad** en A. Suavidad es 1 − rugosidad, así que hay que
    /// componer el mapa; no se puede enchufar directamente.
    ///
    /// También importa la convención de normales: se usa **NormalGL**, no NormalDX, porque
    /// Unity sigue el convenio de OpenGL para el eje Y. Usar el DX deja la iluminación
    /// invertida en relieve, que es un error sutil y difícil de ver si no se busca.
    /// </summary>
    public static class VdfPbrImporter
    {
        public const string CarpetaCC0 = "Assets/_VDF/Textures/CC0";
        public const string CarpetaMateriales = "Assets/_VDF/Materials/PBR";

        /// <summary>Deja una textura lista para leerse por código y con el tipo correcto.</summary>
        private static void ConfigurarImportacion(string ruta, bool esNormal, bool esDatos, bool legible)
        {
            var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (imp == null) return;

            bool cambia = false;

            var tipo = esNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (imp.textureType != tipo) { imp.textureType = tipo; cambia = true; }

            // Rugosidad, metalicidad y normales son DATOS: nunca sRGB.
            bool srgb = !esNormal && !esDatos;
            if (imp.sRGBTexture != srgb) { imp.sRGBTexture = srgb; cambia = true; }

            if (imp.isReadable != legible) { imp.isReadable = legible; cambia = true; }
            if (imp.anisoLevel < 4) { imp.anisoLevel = 4; cambia = true; }

            if (cambia) imp.SaveAndReimport();
        }

        private static Texture2D Cargar(string nombre, bool esNormal, bool esDatos, bool legible)
        {
            string ruta = CarpetaCC0 + "/" + nombre + ".png";
            if (!File.Exists(ruta)) return null;

            ConfigurarImportacion(ruta, esNormal, esDatos, legible);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        }

        /// <summary>
        /// Compone el mapa que pide URP: R = metalicidad, A = suavidad (1 − rugosidad).
        /// Si no hay mapa de metalicidad, se usa el valor fijo que se pase.
        /// </summary>
        private static Texture2D ComponerMascara(string id, float metalicidadPorDefecto)
        {
            string destino = CarpetaCC0 + "/" + id + "_MaskURP.png";

            var rug = Cargar(id + "_Roughness", false, true, true);
            if (rug == null)
            {
                Debug.LogWarning("[VDF] No hay mapa de rugosidad para " + id);
                return null;
            }
            var met = Cargar(id + "_Metalness", false, true, true);

            int w = rug.width, h = rug.height;
            var pix = new Color[w * h];
            var pRug = rug.GetPixels();
            var pMet = met != null ? met.GetPixels() : null;

            for (int i = 0; i < pix.Length; i++)
            {
                float metalicidad = pMet != null ? pMet[i].r : metalicidadPorDefecto;
                float suavidad = 1f - pRug[i].r;
                pix[i] = new Color(metalicidad, 0f, 0f, suavidad);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels(pix);
            tex.Apply();
            File.WriteAllBytes(destino, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceUpdate);
            ConfigurarImportacion(destino, false, true, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(destino);
        }

        /// <summary>
        /// Genera una versión **neutra** del mapa de color: luminancia, con el brillo medio
        /// llevado a un valor conocido.
        ///
        /// Hace falta porque los juegos CC0 vienen con su propio color. `PaintedMetal004` es
        /// pintura ROJA, y un tinte multiplicativo no puede quitar un tono: multiplicar rojo
        /// por gris da rojo apagado, no gris. Pasando a luminancia se conserva **todo el
        /// detalle** —arañazos, desconchones, suciedad, variación— y el tinte sí manda sobre
        /// el color. Así un único juego descargado sirve para el tablero gris, el breaker
        /// oscuro y la carcasa marfil.
        /// </summary>
        private static Texture2D ComponerAlbedoNeutro(string id, float brilloObjetivo, float desgaste = 1f)
        {
            string destino = CarpetaCC0 + "/" + id + "_AlbedoNeutro.png";

            var color = Cargar(id + "_Color", false, false, true);
            if (color == null) return null;

            var pix = color.GetPixels();
            var salida = new Color[pix.Length];

            // Media de luminancia, para poder normalizar el brillo.
            float suma = 0f;
            var lum = new float[pix.Length];
            for (int i = 0; i < pix.Length; i++)
            {
                lum[i] = 0.2126f * pix[i].r + 0.7152f * pix[i].g + 0.0722f * pix[i].b;
                suma += lum[i];
            }
            float media = Mathf.Max(0.001f, suma / pix.Length);
            float escala = brilloObjetivo / media;

            for (int i = 0; i < pix.Length; i++)
            {
                float v = Mathf.Clamp01(lum[i] * escala);
                // `desgaste` acerca el mapa a plano. Los juegos CC0 vienen muy castigados
                // (chapa a la intemperie); un tablero de laboratorio tiene la misma clase de
                // detalle pero mucho mas suave. Bajarlo conserva el relieve del normal y la
                // variacion especular, que es lo que quita el aspecto de plastico de juego.
                v = Mathf.Lerp(1f, v, Mathf.Clamp01(desgaste));
                salida[i] = new Color(v, v, v, 1f);
            }

            var tex = new Texture2D(color.width, color.height, TextureFormat.RGBA32, false, false);
            tex.SetPixels(salida);
            tex.Apply();
            File.WriteAllBytes(destino, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceUpdate);
            ConfigurarImportacion(destino, false, false, false);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(destino);
        }

        /// <summary>
        /// Crea un material URP/Lit a partir de un juego CC0, tintado con el color pedido.
        ///
        /// El tinte multiplica al mapa de color, así que una misma chapa sirve para el
        /// tablero gris, el breaker oscuro y la carcasa marfil del variador sin descargar
        /// tres juegos distintos.
        /// </summary>
        public static Material CrearMaterial(string nombreMaterial, string idCC0, Color tinte,
                                             float escalaUV = 1f, float fuerzaNormal = 1f,
                                             float metalicidadPorDefecto = 0f,
                                             bool neutralizarColor = true,
                                             float brilloBase = 0.78f)
        {
            if (!Directory.Exists(CarpetaMateriales)) Directory.CreateDirectory(CarpetaMateriales);

            var color = neutralizarColor
                ? ComponerAlbedoNeutro(idCC0, brilloBase)
                : Cargar(idCC0 + "_Color", false, false, false);
            var normal = Cargar(idCC0 + "_NormalGL", true, false, false);
            var mascara = ComponerMascara(idCC0, metalicidadPorDefecto);

            string ruta = CarpetaMateriales + "/" + nombreMaterial + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, ruta);
            }

            mat.SetColor("_BaseColor", tinte);
            if (color != null)
            {
                mat.SetTexture("_BaseMap", color);
                mat.SetTextureScale("_BaseMap", Vector2.one * escalaUV);
            }

            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", Vector2.one * escalaUV);
                mat.SetFloat("_BumpScale", fuerzaNormal);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mascara != null)
            {
                mat.SetTexture("_MetallicGlossMap", mascara);
                mat.SetTextureScale("_MetallicGlossMap", Vector2.one * escalaUV);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            else
            {
                mat.SetFloat("_Metallic", metalicidadPorDefecto);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Cambia el mapa de color de todos los materiales PBR ya creados por su versión
        /// neutra, conservando tinte, normales, máscara y escalas. Sirve para corregir sin
        /// tener que reconstruir la escena entera.
        /// </summary>
        public static string RegenerarAlbedosNeutros(float brilloBase = 0.78f, float desgaste = 1f)
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { CarpetaMateriales });
            int tocados = 0;
            var cache = new System.Collections.Generic.Dictionary<string, Texture2D>();

            for (int i = 0; i < guids.Length; i++)
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guids[i]);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);
                if (mat == null) continue;

                // El nombre codifica la familia: VDF_PBR_<idCC0>_<HEX> o VDF_PBR_<Alias>.
                string id = null;
                string[] familias = { "PaintedMetal004", "Plastic010", "Metal027", "Rubber004",
                                      "Metal032", "Metal048A", "Plastic017A", "Plastic013A" };
                for (int f = 0; f < familias.Length; f++)
                    if (mat.name.IndexOf(familias[f], System.StringComparison.Ordinal) >= 0) { id = familias[f]; break; }
                if (id == null) continue;

                Texture2D neutro;
                if (!cache.TryGetValue(id, out neutro))
                {
                    neutro = ComponerAlbedoNeutro(id, brilloBase, desgaste);
                    cache[id] = neutro;
                }
                if (neutro == null) continue;

                Vector2 escala = mat.GetTextureScale("_BaseMap");
                mat.SetTexture("_BaseMap", neutro);
                mat.SetTextureScale("_BaseMap", escala);
                EditorUtility.SetDirty(mat);
                tocados++;
            }

            AssetDatabase.SaveAssets();
            return "materiales con albedo neutralizado: " + tocados + " (familias: " + cache.Count + ")";
        }

        // =====================================================================================
        // Aplicación al tablero
        // =====================================================================================

        /// <summary>A qué familia de superficie pertenece cada pieza del tablero.</summary>
        private static bool FamiliaDe(Transform t, out string idCC0, out float escalaUV,
                                      out float fuerzaNormal, out float metalicidad)
        {
            idCC0 = "PaintedMetal004";
            escalaUV = 2f;
            fuerzaNormal = 1f;
            metalicidad = 0f;

            string nombre = t.name;
            string padre = t.parent != null ? t.parent.name : "";

            // Estas excepciones van PRIMERO, antes de la regla general que mira el tipo del
            // elemento. Puestas al final no servían de nada: la regla general ya había
            // clasificado la pieza como «Bloque» y devuelto chapa metálica.
            //
            // La plancha de la regleta es parte de la misma lámina blanca del tablero, no una
            // pieza de chapa aparte: con textura de metal se veía como una franja gris pegada
            // encima, y en el módulo real no hay tal franja.
            if (padre.StartsWith("Bloque_PLACA_REGLETA")) return false;

            // Las piezas del magnetotérmico: el cuerpo es plástico blanco, las palancas
            // plástico gris y los tornillos metálicos. Como todas cuelgan de un elemento de
            // tipo «Bloque», la regla general las pintaba a todas de chapa y el breaker salía
            // como un amasijo blanco brillante.
            if (padre.StartsWith("Bloque_BREAKER"))
            {
                if (nombre.StartsWith("Tornillo"))
                {
                    idCC0 = "Metal048A"; escalaUV = 1f; metalicidad = 0.9f; return true;
                }
                idCC0 = "Plastic013A"; escalaUV = 1f; fuerzaNormal = 0.3f; metalicidad = 0f;
                return true;
            }

            // Un borne no es una pieza de laton. Lo que se ve de frente es el capuchon de
            // plastico, rojo o negro, y el metal solo asoma dentro del agujero. Pintados
            // enteros de laton niquelado con metalicidad 0,95, los rojos salian negros con un
            // reflejo rojizo y la regleta parecia una fila de tornillos: era la diferencia
            // mas llamativa al poner la captura al lado de la foto del modulo.
            if (padre.StartsWith("Borne"))
            {
                if (nombre == "Collar" || nombre == "Hueco")
                {
                    idCC0 = "Metal048A"; escalaUV = 1f; metalicidad = 0.9f; return true;
                }
                idCC0 = "Plastic017A"; escalaUV = 1f; fuerzaNormal = 0.3f; metalicidad = 0f;
                return true;
            }
            if (nombre == "EjeGeometria" || nombre == "Disco")
            {
                idCC0 = "Metal048A"; escalaUV = 1f; metalicidad = 0.95f; return true;
            }
            if (padre.StartsWith("Borne_") == false && padre.IndexOf("_", System.StringComparison.Ordinal) > 0)
            {
                // Nodos creados por el builder: "Tipo_Id".
                string tipo = padre.Substring(0, padre.IndexOf("_", System.StringComparison.Ordinal));

                if (tipo == "Borne")
                {
                    idCC0 = "Metal048A"; escalaUV = 1f; metalicidad = 0.95f; return true;
                }
                // Pulsadores y pilotos se quedan SIN textura, con su color liso.
                //
                // Llevaban Plastic017A, y la suavidad de un material PBR sale del mapa de
                // la textura, no del valor que pide el constructor: por eso bajarla a 0,16
                // no servia de nada y las lentes seguian con un reflejo blanco que cubria
                // un tercio de la cabeza. Al lado de la foto parecian canicas de cristal.
                // A esta distancia el grano del plastico no se ve; el reflejo si.
                if (tipo == "PilotoLed" || tipo == "Pulsador" || tipo == "PulsadorHongo")
                    return false;
                // Carcasa del variador: plastico mate de grano fino.
                if (tipo == "Variador")
                {
                    idCC0 = "Plastic013A"; escalaUV = 1.5f; fuerzaNormal = 0.5f; return true;
                }
                // Breaker y planchas: chapa satinada, como el tablero.
                if (tipo == "Bloque")
                {
                    idCC0 = "Metal032"; escalaUV = 1.5f; fuerzaNormal = 0.6f; return true;
                }
            }

            // La placa del tablero se queda SIN textura, con su color liso.
            //
            // Primero fue chapa metálica satinada y luego plástico de grano, y en los dos casos
            // salía gris: el albedo de la textura multiplica al tinte, así que un blanco acaba
            // siendo el gris de la foto de la textura. El módulo real es una lámina blanca lisa
            // con la serigrafía impresa, sin grano visible a esta distancia, y eso es
            // exactamente lo que hace un material plano.
            if (nombre == "Placa") return false;
            if (nombre == "Carcasa" || nombre == "Bancada" || nombre == "CajaBornes")
            {
                idCC0 = "Metal032"; escalaUV = 1.5f; fuerzaNormal = 0.7f; return true;
            }
            if (nombre == "Capuchon") { idCC0 = "Plastic017A"; escalaUV = 1f; fuerzaNormal = 0.3f; return true; }

            return false;
        }

        /// <summary>
        /// Sustituye los materiales planos del tablero por versiones PBR, **conservando el
        /// color de cada pieza**. Para no crear un material por objeto, se cachean por
        /// pareja (familia, color): el tablero entero acaba con unas pocas decenas.
        /// </summary>
        public static string AplicarATablero()
        {
            var cache = new System.Collections.Generic.Dictionary<string, Material>();
            var renderers = Object.FindObjectsByType<Renderer>();

            int cambiados = 0, saltados = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];

                // El texto y los cables se dejan como están.
                if (r is LineRenderer) { saltados++; continue; }
                if (r.GetComponent<TMPro.TextMeshPro>() != null) { saltados++; continue; }

                var actual = r.sharedMaterial;
                if (actual == null) { saltados++; continue; }
                if (actual.name.StartsWith("VDF_PBR_")) { saltados++; continue; }

                string idCC0; float escalaUV, fuerzaNormal, metalicidad;
                if (!FamiliaDe(r.transform, out idCC0, out escalaUV, out fuerzaNormal, out metalicidad))
                {
                    saltados++; continue;
                }

                Color tinte = actual.HasProperty("_BaseColor") ? actual.GetColor("_BaseColor") : Color.white;
                string clave = idCC0 + "_" + ColorUtility.ToHtmlStringRGB(tinte);

                Material mat;
                if (!cache.TryGetValue(clave, out mat))
                {
                    mat = CrearMaterial("VDF_PBR_" + clave, idCC0, tinte, escalaUV, fuerzaNormal, metalicidad);
                    cache[clave] = mat;
                }

                r.sharedMaterial = mat;
                cambiados++;
            }

            AssetDatabase.SaveAssets();
            return "renderers repintados: " + cambiados + "   saltados: " + saltados
                 + "   materiales PBR distintos: " + cache.Count;
        }
    }
}
#endif
