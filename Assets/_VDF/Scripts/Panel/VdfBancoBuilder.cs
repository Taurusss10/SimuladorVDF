using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Medidas del puesto de trabajo. Son dato, igual que el layout del tablero: cuando
    /// lleguen las fotos del laboratorio real basta con cambiar números aquí.
    ///
    /// Convenio de ejes, el mismo que <see cref="VdfPanelBuilder"/>: el usuario mira hacia
    /// +Z, así que el borde que tiene delante es el de Z más negativa.
    /// </summary>
    [System.Serializable]
    public class VdfBancoConfig
    {
        [Header("Encimera")]
        [Tooltip("Altura de la superficie de trabajo. Debe coincidir con el plano de colisión " +
                 "de los cables, o los cables se apoyarán en el aire.")]
        public float AlturaSuperficie = 0.75f;

        public float Ancho = 2.20f;
        public float Fondo = 0.80f;
        public float GrosorEncimera = 0.04f;

        [Tooltip("Centro de la mesa en X y en Z. El tablero está centrado en X = 0.")]
        public Vector2 CentroXZ = new Vector2(0f, -0.20f);

        [Header("Patas")]
        public float LadoPata = 0.07f;
        [Tooltip("Cuánto se meten las patas hacia dentro desde la esquina.")]
        public float RetranqueoPatas = 0.10f;

        [Header("Estante inferior")]
        public bool ConEstante = true;
        public float AlturaEstante = 0.20f;

        [Header("Armario del módulo")]
        [Tooltip("Cuánto sobresale el armario por detrás de la placa del tablero.")]
        public float FondoArmario = 0.17f;
        public float GrosorChapa = 0.02f;

        [Header("Colores")]
        public Color ColorEncimera = new Color(0.62f, 0.60f, 0.56f);
        public Color ColorEstructura = new Color(0.28f, 0.30f, 0.33f);
        public Color ColorArmario = new Color(0.52f, 0.54f, 0.56f);
    }

    /// <summary>
    /// Construye el puesto de trabajo: la mesa sobre la que descansa el módulo y el armario
    /// que cierra el tablero por detrás.
    ///
    /// Por qué existe: hasta la fase 2 la "mesa" solo era un plano de colisión invisible para
    /// la física de los cables (<see cref="VdfColisionadorCable"/>). Los cables se apoyaban
    /// correctamente... en el aire, y el PLC y el motor flotaban. Aquí se construye la
    /// geometría que ese plano ya daba por supuesta, de modo que lo que se ve coincida con lo
    /// que la física calcula.
    ///
    /// Todo son cajas y todas comparten tres materiales, para no gastar draw calls en Quest.
    /// </summary>
    public static class VdfBancoBuilder
    {
        public const string NombreRaiz = "VDF_Banco";
        public const string NombreArmario = "VDF_Armario";

        /// <summary>
        /// Construye la mesa. Devuelve la raíz creada.
        /// </summary>
        public static GameObject ConstruirBanco(VdfBancoConfig cfg, Transform padre = null)
        {
            if (cfg == null) cfg = new VdfBancoConfig();

            var raiz = new GameObject(NombreRaiz);
            if (padre != null) raiz.transform.SetParent(padre, false);

            float cx = cfg.CentroXZ.x;
            float cz = cfg.CentroXZ.y;

            // --- Encimera. Su cara SUPERIOR queda exactamente en AlturaSuperficie ----------
            float yEncimera = cfg.AlturaSuperficie - cfg.GrosorEncimera * 0.5f;
            Caja(raiz.transform, "Encimera",
                 new Vector3(cx, yEncimera, cz),
                 new Vector3(cfg.Ancho, cfg.GrosorEncimera, cfg.Fondo),
                 cfg.ColorEncimera, 0.25f);

            // --- Faldón frontal: tapa el canto y da sensación de mueble -------------------
            float zFrente = cz - cfg.Fondo * 0.5f + cfg.GrosorEncimera * 0.5f;
            Caja(raiz.transform, "Faldon",
                 new Vector3(cx, cfg.AlturaSuperficie - cfg.GrosorEncimera - 0.04f, zFrente),
                 new Vector3(cfg.Ancho, 0.08f, cfg.GrosorEncimera),
                 cfg.ColorEstructura, 0.2f);

            // --- Patas --------------------------------------------------------------------
            float alturaPata = cfg.AlturaSuperficie - cfg.GrosorEncimera;
            float dx = cfg.Ancho * 0.5f - cfg.RetranqueoPatas - cfg.LadoPata * 0.5f;
            float dz = cfg.Fondo * 0.5f - cfg.RetranqueoPatas - cfg.LadoPata * 0.5f;

            for (int ix = 0; ix < 2; ix++)
            {
                for (int iz = 0; iz < 2; iz++)
                {
                    float px = cx + (ix == 0 ? -dx : dx);
                    float pz = cz + (iz == 0 ? -dz : dz);
                    Caja(raiz.transform, "Pata_" + ix + iz,
                         new Vector3(px, alturaPata * 0.5f, pz),
                         new Vector3(cfg.LadoPata, alturaPata, cfg.LadoPata),
                         cfg.ColorEstructura, 0.2f);
                }
            }

            // --- Estante inferior ----------------------------------------------------------
            if (cfg.ConEstante)
            {
                Caja(raiz.transform, "Estante",
                     new Vector3(cx, cfg.AlturaEstante, cz),
                     new Vector3(cfg.Ancho - cfg.RetranqueoPatas * 2f, 0.03f, cfg.Fondo - cfg.RetranqueoPatas * 2f),
                     cfg.ColorEstructura, 0.2f);
            }

            return raiz;
        }

        /// <summary>
        /// Cierra el tablero por detrás con un armario: chapa trasera, dos costados y tapa.
        /// Así el módulo deja de ser una placa suelta de pie.
        ///
        /// Se construye a partir de las medidas del layout, no de constantes propias, para que
        /// siga encajando cuando el tablero se reajuste con las fotos reales.
        /// </summary>
        public static GameObject ConstruirArmario(VdfPanelLayout layout, VdfBancoConfig cfg,
                                                  Transform padre = null)
        {
            if (layout == null)
            {
                Debug.LogError("[VDF] VdfBancoBuilder.ConstruirArmario: el layout es null.");
                return null;
            }
            if (cfg == null) cfg = new VdfBancoConfig();

            var raiz = new GameObject(NombreArmario);
            if (padre != null) raiz.transform.SetParent(padre, false);

            float ancho = layout.Ancho;
            float alto = layout.Alto;
            float e = cfg.GrosorChapa;

            // El armario arranca justo detrás de la placa y crece hacia +Z.
            float zInicio = layout.Grosor;
            float zFin = zInicio + cfg.FondoArmario;

            // --- Chapa trasera --------------------------------------------------------------
            Caja(raiz.transform, "Trasera",
                 new Vector3(ancho * 0.5f, alto * 0.5f, zFin - e * 0.5f),
                 new Vector3(ancho, alto, e),
                 cfg.ColorArmario, 0.3f);

            // --- Costados -------------------------------------------------------------------
            float zCentro = (zInicio + zFin) * 0.5f;
            float fondoUtil = zFin - zInicio;

            Caja(raiz.transform, "Costado_Izq",
                 new Vector3(e * 0.5f, alto * 0.5f, zCentro),
                 new Vector3(e, alto, fondoUtil),
                 cfg.ColorArmario, 0.3f);

            Caja(raiz.transform, "Costado_Der",
                 new Vector3(ancho - e * 0.5f, alto * 0.5f, zCentro),
                 new Vector3(e, alto, fondoUtil),
                 cfg.ColorArmario, 0.3f);

            // --- Tapa superior ---------------------------------------------------------------
            Caja(raiz.transform, "Tapa",
                 new Vector3(ancho * 0.5f, alto - e * 0.5f, zCentro),
                 new Vector3(ancho, e, fondoUtil),
                 cfg.ColorArmario, 0.3f);

            return raiz;
        }

        // =====================================================================================

        /// <summary>
        /// Crea una caja con material compartido. Reutiliza la caché de
        /// <see cref="VdfPanelBuilder.ObtenerMaterial"/>, así que repetir un color no añade
        /// materiales nuevos ni rompe el batching.
        /// </summary>
        private static GameObject Caja(Transform padre, string nombre, Vector3 posicionLocal,
                                       Vector3 tamano, Color color, float suavidad)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.localPosition = posicionLocal;
            go.transform.localScale = tamano;

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = VdfPanelBuilder.ObtenerMaterial(color, suavidad);

            return go;
        }
    }
}
