using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Medidas y colores del aula. Son dato: cambiar el laboratorio es cambiar números.
    ///
    /// Convenio de ejes, el mismo que el resto del proyecto: el usuario mira hacia +Z, así
    /// que la pared del fondo (detrás del módulo) es la de Z positiva y la de su espalda la
    /// de Z negativa.
    /// </summary>
    [System.Serializable]
    public class VdfEntornoConfig
    {
        [Header("Dimensiones del aula")]
        public float Ancho = 7.0f;
        [Tooltip("Desde la pared de la espalda del usuario hasta la pared del fondo.")]
        public float Largo = 6.0f;
        public float AlturaTecho = 2.80f;

        [Tooltip("Centro del aula en X y Z. El módulo está en X = 0, Z = 0.")]
        public Vector2 CentroXZ = new Vector2(0f, -0.60f);

        [Header("Acabados")]
        public float GrosorMuro = 0.12f;
        public float AlturaRodapie = 0.10f;
        [Tooltip("Altura a la que arranca el zócalo de pintura de la pared.")]
        public float AlturaZocalo = 1.15f;

        [Header("Luminarias del techo")]
        public bool ConLuminarias = true;
        public int NumeroLuminarias = 2;
        public float LargoLuminaria = 1.20f;
        public float AnchoLuminaria = 0.30f;

        [Header("Ventana (pared izquierda)")]
        public bool ConVentana = false;
        public float AnchoVentana = 2.40f;
        public float AltoVentana = 1.10f;
        [Tooltip("Debe quedar POR ENCIMA de AlturaZocalo, o el hueco corta la banda de " +
                 "pintura del zócalo y la pared se ve partida.")]
        public float AlturaAntepecho = 1.20f;
        [Tooltip("Por la ventana se ve el campus de la Universidad de Pamplona (foto). Sin " +
                 "esto, el cristal es un panel azul liso.")]
        // Apagada: la foto era del campus de Pamplona y el módulo está en la sede de Villa
        // del Rosario, de la que no hay fotos libres. Se enciende cuando llegue una foto de
        // esa sede (sustituir Textures/VistaCampusUnipamplona.jpg).
        public bool ConVistaExterior = false;

        [Header("Colores")]
        public Color ColorSuelo = new Color(0.58f, 0.59f, 0.60f);
        public Color ColorParedAlta = new Color(0.95f, 0.95f, 0.93f);
        public Color ColorZocalo = new Color(0.80f, 0.81f, 0.82f);
        public Color ColorRodapie = new Color(0.36f, 0.37f, 0.39f);

        [Header("Identidad de la Universidad de Pamplona")]
        [Tooltip("Franja fina en lo alto del zócalo, en el rojo del escudo.")]
        public bool ConFranja = true;
        public float AltoFranja = 0.05f;
        public Color ColorFranja = new Color(0.72f, 0.13f, 0.12f);
        [Tooltip("Escudo de la Universidad de Pamplona en la pared del fondo.")]
        public bool ConEscudo = true;
        public float AltoEscudo = 1.00f;
        [Tooltip("Centro del escudo sobre la pared del fondo: x y altura.")]
        public Vector2 PosicionEscudo = new Vector2(1.60f, 1.95f);
        public Color ColorTecho = new Color(0.90f, 0.90f, 0.90f);
        public Color ColorLuminaria = new Color(0.95f, 0.96f, 0.92f);
        public Color ColorCristal = new Color(0.62f, 0.74f, 0.82f);
        public Color ColorMarco = new Color(0.25f, 0.26f, 0.28f);
    }

    /// <summary>
    /// Construye el aula alrededor del módulo: suelo, cuatro paredes con zócalo y rodapié,
    /// techo con luminarias y una ventana lateral.
    ///
    /// Por qué existe: con el módulo solo sobre un plano infinito gris, la escena se leía como
    /// una maqueta flotando en la nada. Un aula cerrada da escala, da referencias al ojo (que
    /// en VR es lo que evita la sensación de estar suspendido) y da superficies donde rebote
    /// la luz.
    ///
    /// Alcance deliberado: sin fotorrealismo, como pide el enunciado. Son cajas con colores
    /// planos compartidos. El coste es de unas 20 cajas y 8 materiales, que en Quest es nada,
    /// y el salto visual es grande.
    ///
    /// Las paredes se construyen HACIA FUERA del aula: la cara que ve el usuario está en la
    /// medida interior exacta, y el grosor crece hacia el exterior. Así cambiar el grosor no
    /// mueve las paredes de sitio.
    /// </summary>
    public static class VdfEntornoBuilder
    {
        public const string NombreRaiz = "VDF_Entorno";

        public static GameObject Construir(VdfEntornoConfig cfg, Transform padre = null)
        {
            if (cfg == null) cfg = new VdfEntornoConfig();

            var raiz = new GameObject(NombreRaiz);
            if (padre != null) raiz.transform.SetParent(padre, false);

            float cx = cfg.CentroXZ.x;
            float cz = cfg.CentroXZ.y;
            float e = cfg.GrosorMuro;
            float h = cfg.AlturaTecho;

            // Caras interiores del aula.
            float xIzq = cx - cfg.Ancho * 0.5f;
            float xDer = cx + cfg.Ancho * 0.5f;
            float zFrente = cz - cfg.Largo * 0.5f;   // a la espalda del usuario
            float zFondo = cz + cfg.Largo * 0.5f;    // detrás del módulo

            // Suelo y techo se hacen MÁS GRANDES que el interior, para que solapen con las
            // paredes en lugar de tocarlas justo. Al construirlos a medida exacta, la junta
            // techo-pared dejaba pasar una línea de luz diagonal del sol: dos superficies
            // que coinciden al milímetro no sellan, porque el sesgo de sombra las separa.
            float solape = e * 2f;

            // --- Suelo ----------------------------------------------------------------------
            Caja(raiz.transform, "Suelo",
                 new Vector3(cx, -e * 0.5f, cz),
                 new Vector3(cfg.Ancho + solape, e, cfg.Largo + solape),
                 cfg.ColorSuelo, 0.15f);

            // --- Techo ----------------------------------------------------------------------
            Caja(raiz.transform, "Techo",
                 new Vector3(cx, h + e * 0.5f, cz),
                 new Vector3(cfg.Ancho + solape, e, cfg.Largo + solape),
                 cfg.ColorTecho, 0.05f);

            // --- Paredes --------------------------------------------------------------------
            // Fondo y frente abarcan todo el ancho, incluido el grosor de las laterales.
            float anchoTotal = cfg.Ancho + e * 2f;

            ConstruirParedZ(raiz.transform, "Pared_Fondo", cfg, cx, zFondo + e * 0.5f, anchoTotal, h, e, false);
            ConstruirParedZ(raiz.transform, "Pared_Frente", cfg, cx, zFrente - e * 0.5f, anchoTotal, h, e, true);

            ConstruirParedX(raiz.transform, "Pared_Izq", cfg, xIzq - e * 0.5f, cz, cfg.Largo, h, e, cfg.ConVentana);
            ConstruirParedX(raiz.transform, "Pared_Der", cfg, xDer + e * 0.5f, cz, cfg.Largo, h, e, false);

            // --- Franja roja y escudo ---------------------------------------------------------
            if (cfg.ConFranja)
            {
                float yF = cfg.AlturaZocalo + cfg.AltoFranja * 0.5f;
                float g = 0.012f;   // sobresale un poco de la pared para que no parpadee
                Caja(raiz.transform, "Franja_Fondo", new Vector3(cx, yF, zFondo - g * 0.5f),
                     new Vector3(cfg.Ancho, cfg.AltoFranja, g), cfg.ColorFranja, 0.2f);
                Caja(raiz.transform, "Franja_Frente", new Vector3(cx, yF, zFrente + g * 0.5f),
                     new Vector3(cfg.Ancho, cfg.AltoFranja, g), cfg.ColorFranja, 0.2f);
                Caja(raiz.transform, "Franja_Der", new Vector3(xDer - g * 0.5f, yF, cz),
                     new Vector3(g, cfg.AltoFranja, cfg.Largo), cfg.ColorFranja, 0.2f);
                // En la izquierda, a los lados de la ventana: el antepecho está por encima.
                Caja(raiz.transform, "Franja_Izq", new Vector3(xIzq + g * 0.5f, yF, cz),
                     new Vector3(g, cfg.AltoFranja, cfg.Largo), cfg.ColorFranja, 0.2f);
            }

            if (cfg.ConEscudo) ColocarEscudo(raiz.transform, cfg, zFondo);

            // --- Luminarias ------------------------------------------------------------------
            if (cfg.ConLuminarias && cfg.NumeroLuminarias > 0)
            {
                float paso = cfg.Largo / (cfg.NumeroLuminarias + 1);
                for (int i = 0; i < cfg.NumeroLuminarias; i++)
                {
                    float pz = zFrente + paso * (i + 1);
                    var lum = Caja(raiz.transform, "Luminaria_" + (i + 1),
                                   new Vector3(cx, h - 0.03f, pz),
                                   new Vector3(cfg.LargoLuminaria, 0.06f, cfg.AnchoLuminaria),
                                   cfg.ColorLuminaria, 0.1f);

                    // Emisiva: no ilumina de verdad (de eso se encarga VdfIluminacion), pero
                    // sin esto la luminaria se ve como una caja apagada en el techo. Con el
                    // material de los pilotos no valía: ese hornea la emisión en negro, y con
                    // el techo ya brillante la luminaria salía más oscura que el techo.
                    lum.GetComponent<Renderer>().sharedMaterial =
                        MaterialDeSala(cfg.ColorLuminaria, 0.1f, 1.2f);
                }
            }

            return raiz;
        }

        // =====================================================================================
        // Paredes
        // =====================================================================================

        /// <summary>Pared perpendicular a Z (fondo o frente), con zócalo y rodapié.</summary>
        private static void ConstruirParedZ(Transform padre, string nombre, VdfEntornoConfig cfg,
                                            float cx, float pz, float ancho, float alto,
                                            float grosor, bool esFrente)
        {
            float yZocalo = cfg.AlturaZocalo;

            Caja(padre, nombre + "_Zocalo",
                 new Vector3(cx, yZocalo * 0.5f, pz),
                 new Vector3(ancho, yZocalo, grosor),
                 cfg.ColorZocalo, 0.2f);

            Caja(padre, nombre + "_Alta",
                 new Vector3(cx, (alto + yZocalo) * 0.5f, pz),
                 new Vector3(ancho, alto - yZocalo, grosor),
                 cfg.ColorParedAlta, 0.05f);

            // El rodapié solo se ve desde dentro, así que se pega a la cara interior.
            float signo = esFrente ? 1f : -1f;
            Caja(padre, nombre + "_Rodapie",
                 new Vector3(cx, cfg.AlturaRodapie * 0.5f, pz + signo * (grosor * 0.5f + 0.008f)),
                 new Vector3(ancho, cfg.AlturaRodapie, 0.016f),
                 cfg.ColorRodapie, 0.25f);
        }

        /// <summary>Pared perpendicular a X (lateral), opcionalmente con ventana.</summary>
        private static void ConstruirParedX(Transform padre, string nombre, VdfEntornoConfig cfg,
                                            float px, float cz, float largo, float alto,
                                            float grosor, bool conVentana)
        {
            float yZocalo = cfg.AlturaZocalo;

            Caja(padre, nombre + "_Zocalo",
                 new Vector3(px, yZocalo * 0.5f, cz),
                 new Vector3(grosor, yZocalo, largo),
                 cfg.ColorZocalo, 0.2f);

            if (!conVentana)
            {
                Caja(padre, nombre + "_Alta",
                     new Vector3(px, (alto + yZocalo) * 0.5f, cz),
                     new Vector3(grosor, alto - yZocalo, largo),
                     cfg.ColorParedAlta, 0.05f);
            }
            else
            {
                // La pared alta se parte en cuatro piezas alrededor del hueco, en vez de
                // taladrar la malla: con cajas sale más barato y queda igual de limpio.
                float yInf = cfg.AlturaAntepecho;
                float ySup = cfg.AlturaAntepecho + cfg.AltoVentana;
                float mitadVentana = cfg.AnchoVentana * 0.5f;

                // Antepecho (entre el zócalo y el alféizar).
                if (yInf > yZocalo)
                {
                    Caja(padre, nombre + "_Antepecho",
                         new Vector3(px, (yInf + yZocalo) * 0.5f, cz),
                         new Vector3(grosor, yInf - yZocalo, cfg.AnchoVentana),
                         cfg.ColorParedAlta, 0.05f);
                }

                // Dintel (por encima de la ventana).
                Caja(padre, nombre + "_Dintel",
                     new Vector3(px, (alto + ySup) * 0.5f, cz),
                     new Vector3(grosor, alto - ySup, cfg.AnchoVentana),
                     cfg.ColorParedAlta, 0.05f);

                // Jambas (a los lados del hueco), que cubren el resto del largo.
                float restante = (largo - cfg.AnchoVentana) * 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    float pz = cz + (i == 0 ? -1f : 1f) * (mitadVentana + restante * 0.5f);
                    Caja(padre, nombre + "_Jamba_" + i,
                         new Vector3(px, (alto + yZocalo) * 0.5f, pz),
                         new Vector3(grosor, alto - yZocalo, restante),
                         cfg.ColorParedAlta, 0.05f);
                }

                // Cristal y marco.
                Caja(padre, nombre + "_Cristal",
                     new Vector3(px, (yInf + ySup) * 0.5f, cz),
                     new Vector3(grosor * 0.35f, cfg.AltoVentana, cfg.AnchoVentana),
                     cfg.ColorCristal, 0.9f);

                if (cfg.ConVistaExterior)
                    ColocarVista(padre, cfg, px + (px < 0f ? 1f : -1f) * (grosor * 0.5f - 0.03f),
                                 (yInf + ySup) * 0.5f, cz, px < 0f);

                Caja(padre, nombre + "_Marco_Sup",
                     new Vector3(px, ySup, cz),
                     new Vector3(grosor * 1.1f, 0.05f, cfg.AnchoVentana),
                     cfg.ColorMarco, 0.3f);

                Caja(padre, nombre + "_Alfeizar",
                     new Vector3(px, yInf, cz),
                     new Vector3(grosor * 1.3f, 0.05f, cfg.AnchoVentana),
                     cfg.ColorMarco, 0.3f);
            }

            float signo = px < 0f ? 1f : -1f;
            Caja(padre, nombre + "_Rodapie",
                 new Vector3(px + signo * (grosor * 0.5f + 0.008f), cfg.AlturaRodapie * 0.5f, cz),
                 new Vector3(0.016f, cfg.AlturaRodapie, largo),
                 cfg.ColorRodapie, 0.25f);
        }

        // =====================================================================================
        // Escudo
        // =====================================================================================

        public const string RutaEscudo = "Assets/_VDF/Textures/EscudoUnipamplona.png";
        public const string RutaMaterialEscudo = "Assets/_VDF/Materials/VDF_EscudoUnipamplona.mat";

        /// <summary>
        /// Cuelga el escudo de la Universidad de Pamplona en la pared del fondo.
        ///
        /// La imagen es la de Wikimedia Commons (dominio público). Va sin transparencia: el
        /// fondo se rellenó con el color de la pared, porque en la Quest un material
        /// transparente cuesta más y aquí no aporta nada.
        /// </summary>
        private static void ColocarEscudo(Transform padre, VdfEntornoConfig cfg, float zFondo)
        {
            Material mat = null;
#if UNITY_EDITOR
            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialEscudo);
            if (mat == null)
            {
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(RutaEscudo);
                if (tex == null) return;
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader) { name = "VDF_EscudoUnipamplona" };
                mat.SetTexture("_BaseMap", tex);
                mat.SetTexture("_MainTex", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.05f);
                mat.SetFloat("_Metallic", 0f);
                UnityEditor.AssetDatabase.CreateAsset(mat, RutaMaterialEscudo);
            }
            // El mismo brillo propio que la pared, o su fondo se ve como un cuadro gris.
            var texEscudo = mat.GetTexture("_BaseMap");
            if (mat.GetTexture("_EmissionMap") != texEscudo)
            {
                mat.SetTexture("_EmissionMap", texEscudo);
                mat.SetColor("_EmissionColor", Color.white * BrilloParedes);
                UnityEditor.EditorUtility.SetDirty(mat);
            }
            AsegurarEmision(mat);
#endif
            if (mat == null) return;

            float proporcion = 768f / 1022f;
            var escudo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            escudo.name = "Escudo_Unipamplona";
            escudo.transform.SetParent(padre, false);
            // El quad se ve desde -Z, que es desde donde mira el alumno.
            escudo.transform.localPosition = new Vector3(cfg.PosicionEscudo.x, cfg.PosicionEscudo.y, zFondo - 0.006f);
            escudo.transform.localScale = new Vector3(cfg.AltoEscudo * proporcion, cfg.AltoEscudo, 1f);
            var col = escudo.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col); }
            escudo.GetComponent<Renderer>().sharedMaterial = mat;
        }

        public const string RutaVista = "Assets/_VDF/Textures/VistaCampusUnipamplona.jpg";
        public const string RutaMaterialVista = "Assets/_VDF/Materials/VDF_VistaCampus.mat";

        private static void ColocarVista(Transform padre, VdfEntornoConfig cfg, float x, float y,
                                         float z, bool paredIzquierda)
        {
            Material mat = null;
#if UNITY_EDITOR
            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialVista);
            if (mat == null)
            {
                var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(RutaVista);
                if (tex == null) return;
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Texture");
                mat = new Material(shader) { name = "VDF_VistaCampus" };
                mat.SetTexture("_BaseMap", tex);
                mat.SetTexture("_MainTex", tex);
                mat.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
                UnityEditor.AssetDatabase.CreateAsset(mat, RutaMaterialVista);
            }
#endif
            if (mat == null) return;

            var vista = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vista.name = "Vista_Campus";
            vista.transform.SetParent(padre, false);
            vista.transform.localPosition = new Vector3(x, y, z);
            // El quad se ve desde -Z; girado ±90° mira hacia dentro del aula.
            vista.transform.localRotation = Quaternion.Euler(0f, paredIzquierda ? -90f : 90f, 0f);
            vista.transform.localScale = new Vector3(cfg.AnchoVentana, cfg.AltoVentana, 1f);
            var col = vista.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col); }
            var r = vista.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Borra el aula que haya en la escena y la vuelve a levantar con la configuración
        /// por defecto. Lo llama «Reconstruir las dos escenas».
        /// </summary>
        public static GameObject Reconstruir()
        {
            var previa = GameObject.Find(NombreRaiz);
            if (previa != null)
            {
                if (Application.isPlaying) Object.Destroy(previa); else Object.DestroyImmediate(previa);
            }
            return Construir(new VdfEntornoConfig());
        }

        // =====================================================================================

        /// <summary>
        /// Caja con material compartido, de la misma caché que el tablero y el banco.
        /// </summary>
        private static GameObject Caja(Transform padre, string nombre, Vector3 posicion,
                                       Vector3 tamano, Color color, float suavidad)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicion;
            go.transform.localScale = tamano;

            // Paredes, zócalo y techo llevan un brillo propio: ver MaterialDeSala.
            bool sala = nombre.Contains("_Alta") || nombre.Contains("_Zocalo") || nombre.Contains("_Jamba")
                        || nombre.Contains("_Antepecho") || nombre.Contains("_Dintel") || nombre == "Techo";
            go.GetComponent<Renderer>().sharedMaterial = sala
                ? MaterialDeSala(color, suavidad, nombre == "Techo" ? BrilloTecho : BrilloParedes)
                : VdfPanelBuilder.ObtenerMaterial(color, suavidad);

            return go;
        }

        /// <summary>Fracción de su propio color que emiten las paredes y el techo.</summary>
        public const float BrilloParedes = 0.45f;
        public const float BrilloTecho = 0.55f;

        private static Material MaterialDeSala(Color color, float suavidad, float brillo)
        {
            string nombre = "VDF_Sala_" + ColorUtility.ToHtmlStringRGB(color) + "_B" + Mathf.RoundToInt(brillo * 100f);
#if UNITY_EDITOR
            string ruta = "Assets/_VDF/Materials/" + nombre + ".mat";
            var existente = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null) { AsegurarEmision(existente); return existente; }
#endif
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = nombre };
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Smoothness", suavidad);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * brillo);
            // NO «None»: URP, al validar el material, marca como EmissiveIsBlack todo el que
            // no diga que emite, y entonces le quita _EMISSION. Pasaba al reabrir la escena.
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.CreateAsset(mat, ruta);
            AsegurarEmision(mat);
#endif
            return mat;
        }

        /// <summary>
        /// Al validar el material, URP le quitaba la palabra clave _EMISSION porque sus
        /// banderas de iluminación global decían que no emite: la emisión no se veía aunque
        /// el color estuviera puesto, y la primera versión salió idéntica a la de antes.
        /// </summary>
        private static void AsegurarEmision(Material mat)
        {
            if (mat.IsKeywordEnabled("_EMISSION") &&
                mat.globalIlluminationFlags == MaterialGlobalIlluminationFlags.RealtimeEmissive) return;
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.EnableKeyword("_EMISSION");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(mat);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(mat);
#endif
        }
    }
}
