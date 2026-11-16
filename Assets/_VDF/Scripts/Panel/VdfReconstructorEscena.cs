
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Vuelve a montar el módulo en la escena abierta con las medidas de catálogo.
    ///
    /// Por qué existe: el tablero y el PLC se construyen por código a partir del layout, pero
    /// una vez construidos quedan guardados dentro de la escena. Cuando el layout cambió para
    /// pasar de las medidas inventadas del principio (el V1000 medía 190 x 300 mm) a las de
    /// catálogo (68 x 128), la escena de escritorio se reconstruyó a mano desde la consola y la
    /// de VR se quedó como estaba: 110 x 85 cm de tablero y sockets de 30 mm de radio, es decir,
    /// cada socket agarrando también a sus dos bornes vecinos. El defecto sobrevivió porque el
    /// desarrollador no tiene visor y nadie volvió a abrir esa escena.
    ///
    /// Reconstruir a mano desde la consola no vale: hay que repetirlo en las dos escenas y en
    /// cada rama de entrega, y basta olvidar un paso (reaplicar el PBR, por ejemplo) para que
    /// una entrega se vea distinta de la siguiente. Aquí está la secuencia completa y en orden,
    /// una sola vez.
    ///
    /// Las posiciones son comunes a los dos modos a propósito: la regla 4 del proyecto pide que
    /// todo funcione igual en VR y en escritorio, y si cada escena coloca el módulo en un sitio
    /// distinto, lo que se valida en uno no dice nada del otro.
    /// </summary>
    public static class VdfReconstructorEscena
    {
        /// <summary>Nombre del nodo del PLC en la escena.</summary>
        public const string NombrePlc = "VDF_PLC";

        public const VdfModoMando ModoMandoDeLasEscenas = VdfModoMando.Interruptores;

        public static readonly Vector3 PosicionTablero =
            new Vector3(-0.25f, AlturaEncimera, -0.30f);

        /// <summary>Altura de la encimera del banco, que es donde se apoya todo.</summary>
        public const float AlturaEncimera = 0.75f;

        /// <summary>
        /// Dónde se coloca al alumno, en z. FUERA del bloque UNITY_EDITOR a propósito: la
        /// usa también VdfXrSetup en tiempo de ejecución, y dentro del bloque el APK no
        /// compilaba («no contiene una definición para ZDelAlumno»), aunque los tests, que
        /// corren en el editor, pasaban.
        /// </summary>
        public const float ZDelAlumno = -0.90f;

        public const float InclinacionPlc = 15f;

        public static readonly Vector3 PosicionPlc =
            new Vector3(0.32f, AlturaEncimera, -0.40f);
        public const float GiroPlc = 38f;

        public const float EscalaPlc = 1.6f;

        /// <summary>
        /// El motor, sobre la encimera y a la izquierda. Aquí el origen sí es el centro de la
        /// carcasa, no la esquina, así que lleva media altura de más para apoyarse.
        /// </summary>
        public static readonly Vector3 PosicionMotor =
            new Vector3(-0.46f, AlturaEncimera + 0.07f, -0.38f);

        /// <summary>De dónde salen los cables de VR antes de que alguien los coja.</summary>
        public static readonly Vector3 BandejaCables =
            new Vector3(-0.15f, AlturaEncimera + 0.04f, -0.42f);

        /// <summary>Cuántos cables se dejan en la bandeja; el cableado correcto pide 19.</summary>
        public const int CablesEnBandeja = 20;

#if UNITY_EDITOR
        // ---------------------------------------------------------------------------------
        // A partir de aqui, SOLO EDITOR.
        //
        // Reconstruir llama a VdfIluminacion y a VdfPbrImporter, que viven bajo UNITY_EDITOR
        // porque crean y guardan assets. La clase entera no se puede marcar asi: la constante
        // AlturaEncimera la lee VdfCableSpawner en ejecucion.
        //
        // Sin esta guarda el proyecto compila en el editor pero NO se puede compilar un
        // player: el build de Windows fallaba con «VdfIluminacion does not exist in the
        // current context», y no se habia detectado porque hasta hoy no se habia generado
        // ningun instalable desde que existe el reconstructor.
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Rehace el módulo entero. Devuelve el resumen, que es lo que se pega en la
        /// documentación de la etapa.
        /// </summary>
        /// <param name="conXr">
        /// Si la escena lleva equipo de VR, rehace también los sockets y los cables agarrables.
        /// </param>
        public static string Reconstruir(bool conXr)
        {
            var sb = new System.Text.StringBuilder();

            Borrar(VdfPanelBuilder.NombreRaiz);
            Borrar(NombrePlc);
            Borrar(VdfPanelBuilder.NombreMotor);

            // El grabador de video, si quedo de la ultima vez que se grabo. El menu de
            // grabar lo crea y guarda la escena con el dentro, asi que sin esto se queda
            // ahi para siempre y acaba en el instalable.

            Borrar("XR Interaction Simulator");
            Borrar("XR Interaction Simulator(Clone)");
            Borrar("XR Interaction Simulator UI");
            Borrar("XR Interaction Simulator UI(Clone)");

            // --- Tablero ---------------------------------------------------------------
            var layout = VdfPanelLayoutDefaults.Crear();
            var tablero = VdfPanelBuilder.Construir(layout);
            tablero.transform.position = PosicionTablero;
            sb.AppendLine("tablero: " + (layout.Ancho * 100f).ToString("0.0") + " x "
                          + (layout.Alto * 100f).ToString("0.0") + " cm, "
                          + layout.Elementos.Count + " elementos");

            // El armario cierra el tablero por detrás y se dimensiona desde el layout, así que
            // tiene que construirse DESPUÉS de reconstruir el tablero, no antes.
            VdfBancoBuilder.ConstruirArmario(layout, new VdfBancoConfig(), tablero.transform);

            // --- PLC y motor -----------------------------------------------------------
            var plc = VdfPanelBuilder.Construir(VdfPanelLayoutDefaults.CrearPlc());
            plc.name = NombrePlc;
            plc.transform.position = PosicionPlc;
            plc.transform.rotation = Quaternion.Euler(InclinacionPlc, GiroPlc, 0f);
            plc.transform.localScale = Vector3.one * EscalaPlc;
            // Al escalarlo crece también hacia abajo y se hundía 3 cm en la mesa.
            ApoyarSobreEncimera(plc, 0f);

            VdfPanelBuilder.ConstruirMotor(PosicionMotor);
            sb.AppendLine("PLC y motor recolocados");

            // --- Interruptores (modo VdfModoMando.Interruptores) ------------------------
            // Se construyen siempre y VdfSimulacion los enciende solo en ese modo. Se borran
            // también si quedaron apagados, que GameObject.Find no los ve.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.parent == null && t.name == VdfSimulacion.NombreInterruptores)
                    Matar(t.gameObject);
            var interruptores = VdfInterruptoresBuilder.Construir();
            ColocarInterruptoresSobrePlc(interruptores, plc);
            sb.AppendLine("interruptores S4, S5 y S6 encima del PLC");

            var simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            if (simulacion != null && simulacion.ModoMando != ModoMandoDeLasEscenas)
            {
                simulacion.ModoMando = ModoMandoDeLasEscenas;
                sb.AppendLine("modo de mando: " + ModoMandoDeLasEscenas);
            }

            var variador = BuscarEnTablero(tablero.transform, "Variador_V1000");
            if (variador != null)
            {
                var anclajePantalla = variador.Find("AnclajePantalla");
                var anclajeTeclado = variador.Find("AnclajeTeclado");
                if (anclajePantalla != null && anclajeTeclado != null)
                {
                    var vista = VfdKeypadView.Construir(anclajePantalla, anclajeTeclado);
                    vista.InstalarSieteSegmentos();
                    sb.AppendLine("teclado del V1000 montado sobre los anclajes del variador");
                }
            }

            VdfEntornoBuilder.Reconstruir();
            sb.AppendLine("aula reconstruida, con el escudo de la Universidad de Pamplona");
            VdfIluminacion.Montar(tablero.transform);
            sb.AppendLine(VdfPbrImporter.AplicarATablero());
            VdfColisionBuilder.Montar();

            // --- Dejar caer los cables -------------------------------------------------
            sb.AppendLine(AsentarCables());

            // --- Atriles de las prácticas ----------------------------------------------
            sb.AppendLine(ColocarAtriles(conXr ? EscalaAtrilesVr : 1f));

            // --- Diagrama Ladder -------------------------------------------------------
            sb.AppendLine(ColocarLadder());

            // --- Modo escritorio -------------------------------------------------------
            if (!conXr) sb.AppendLine(EncuadrarCamaraEscritorio());

            // La bienvenida, lo primero que ve quien entra. Va delante del alumno y algo
            // por encima del tablero, para que no haya que buscarla: es la unica pantalla
            // que tiene que leer alguien que nunca se ha puesto un visor.
            //
            // Se construye AQUI, antes del montaje de XR, y eso no es casual: VdfXrSetup
            // recorre los botones que existen en ese momento para darles su puente de XR.
            // Creandola despues, su boton era el unico del simulador que no respondia al
            // mando... justo el que hay que pulsar para empezar.
            // Donde va la bienvenida depende del modo, y hubo que aprenderlo dos veces.
            //
            // En VR, a 33 cm del alumno y DELANTE del tablero: el tablero empieza en
            // z = -0,36 y el primer intento la puso en -0,18, o sea metida dentro del modulo,
            // donde no se veia.
            //
            // En escritorio NO vale la misma altura. La camara se queda a 0,88 m para
            // encuadrar el tablero entero, mientras que en el visor los ojos estan a 1,36:
            // el panel de VR le quedaba por encima del borde de la pantalla, fuera de cuadro.
            // Asi que en escritorio se cuelga delante de la camara, usando su propia
            // orientacion, que es la que acaba de calcular EncuadrarCamaraEscritorio.
            Vector3 dondeVaLaBienvenida;
            if (conXr)
            {
                dondeVaLaBienvenida = new Vector3(0f, 1.22f, ZDelAlumno + 0.33f);
            }
            else
            {
                var camaraEscritorio = Camera.main;
                // Delante de la camara y UN POCO MAS ARRIBA, para que el panel entero
                // quede por encima de la encimera (0,75 m) en vez de medio enterrado en ella.
                dondeVaLaBienvenida = camaraEscritorio != null
                    ? camaraEscritorio.transform.position
                      + camaraEscritorio.transform.forward * 0.55f
                      + Vector3.up * 0.10f
                    : new Vector3(0f, 0.98f, -0.43f);
            }

            sb.AppendLine(VdfPanelBienvenida.Construir(dondeVaLaBienvenida, conXr) != null
                ? "Panel de bienvenida puesto en " + dondeVaLaBienvenida.ToString("F2")
                : "ATENCION: sin bienvenida");

            // --- Lo propio de VR -------------------------------------------------------
            if (conXr)
            {
                var puente = Object.FindAnyObjectByType<VdfXrWiring>();
                if (puente == null)
                {
                    sb.AppendLine("ATENCION: no hay VdfXrWiring en la escena, sin sockets ni cables");
                }
                else
                {
                    BorrarCablesViejos();
                    sb.AppendLine(VdfXrSetup.Montar(puente, BandejaCables, CablesEnBandeja));
                    puente.Reindexar();
                }

                // Y los puestos de trabajo, que hasta hoy NO los tocaba nadie al reconstruir.
                //
                // Se crearon una vez y se quedaron guardados en la escena, asi que cambiar
                // sus posiciones en el codigo no cambiaba nada: seguian donde estaban. Es el
                // mismo patron que el grabador y el simulador de dispositivo, la escena
                // guardando datos que el codigo ya no gobierna, y por eso al traer el tablero
                // hacia el alumno los cuatro discos se quedaron en el sitio viejo.
                sb.AppendLine(VdfXrLocomocion.Montar());
            }

            sb.AppendLine(QuitarSombrasDeLasPiezasPequenas());
            if (conXr) sb.AppendLine(ColocarAlAlumno());

            return sb.ToString();
        }

        /// <summary>Donde se planta el alumno al entrar en VR, en Z.</summary>

        private static string ColocarAlAlumno()
        {
            GameObject rig = null;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
                if (go.transform.parent == null && go.name.Contains("XR Origin")) { rig = go; break; }

            if (rig == null) return "ATENCION: no hay equipo de VR que colocar";

            var antes = rig.transform.position;
            rig.transform.position = new Vector3(0f, 0f, ZDelAlumno);
            rig.transform.rotation = Quaternion.identity;
            return "Alumno colocado en z=" + ZDelAlumno.ToString("0.00")
                 + " (estaba en " + antes.z.ToString("0.00") + ")";
        }

        /// <summary>Por debajo de esto, la sombra de una pieza no se ve.</summary>
        private const float TamanoSinSombra = 0.03f;

        /// <summary>
        /// Quita la proyección de sombra a las piezas pequeñas del tablero.
        ///
        /// Por qué. Una sola luz de la escena proyecta sombras, pero la alimentaban 547
        /// renderers, y 369 de ellos medían menos de 3 cm: bornes, huecos, marcos y
        /// etiquetas, todos pegados a una lámina plana. Cada uno se dibuja otra vez al
        /// generar el mapa de sombras, y la sombra que produce un borne de 2 cm contra la
        /// chapa que tiene detrás no se ve.
        ///
        /// En el computador sobra potencia y no se nota. En una Quest 3S, que renderiza dos
        /// veces por fotograma a 72 Hz o más, el paso de sombras es de lo primero que se
        /// nota. Esto es la regla 7 del contrato —rendimiento para Quest— aplicada donde de
        /// verdad cuesta, y es reversible: cambiar la constante a 0 lo deja como estaba.
        ///
        /// No se tocan las piezas grandes: la lámina, el variador, el breaker, el motor, el
        /// PLC, la mesa y el aula siguen proyectando, que son las que dan volumen a la escena.
        /// </summary>
        private static string QuitarSombrasDeLasPiezasPequenas()
        {
            int quitadas = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
                if (r.bounds.size.magnitude >= TamanoSinSombra) continue;

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quitadas++;
            }
            return "Sombras quitadas a " + quitadas + " piezas de menos de "
                 + (TamanoSinSombra * 100f).ToString("0") + " cm (no se ven y cuestan en el visor)";
        }

        /// <summary>
        /// Deja que los cables de la escena caigan hasta quedarse quietos.
        ///
        /// Hace falta porque en una escena guardada no corre ninguna simulación: los cables se
        /// quedan con el trazado con el que nacen, que es una curva calculada e idéntica en
        /// todos, y se ven tiesos, como varillas dobladas con plantilla. Dejándolos caer aquí
        /// y guardando el resultado, la escena se abre con los cables ya colgando en catenaria
        /// y cada uno a su manera.
        /// </summary>
        public static string AsentarCables()
        {
            var cables = Object.FindObjectsByType<VdfCablePhysics>();
            for (int i = 0; i < cables.Length; i++)
            {
                if (cables[i] == null) continue;
                cables[i].Asentar();
            }
            return "cables asentados: " + cables.Length;
        }

        public const float EscalaAtrilesVr = 1.42f;

        public static void ColocarInterruptoresSobrePlc(GameObject caja, GameObject plc)
        {
            if (caja == null || plc == null) return;
            var b = new Bounds(plc.transform.position, Vector3.zero);
            bool hay = false;
            foreach (var r in plc.GetComponentsInChildren<Renderer>())
            {
                if (!hay) { b = r.bounds; hay = true; } else b.Encapsulate(r.bounds);
            }
            caja.transform.rotation = Quaternion.Euler(VdfInterruptoresBuilder.Giro.x, GiroPlc, 0f);
            // Un poco por encima, para que la caja no se meta en el PLC al inclinarla.
            caja.transform.position = new Vector3(b.center.x, b.max.y + 0.005f, b.center.z);
        }

        public static void ApoyarSobreEncimera(GameObject pieza, float holgura)
        {
            if (pieza == null) return;
            bool hay = false;
            float minY = float.MaxValue;
            foreach (var r in pieza.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer || !r.enabled) continue;
                minY = Mathf.Min(minY, r.bounds.min.y);
                hay = true;
            }
            if (!hay) return;
            float falta = AlturaEncimera + holgura - minY;
            if (falta > 0f) pieza.transform.position += Vector3.up * falta;
        }

        public static string ColocarAtriles(float escala = 1f)
        {
            var layout = VdfPanelLayoutDefaults.Crear();
            float centroX = PosicionTablero.x + layout.Ancho * 0.5f;
            float alturaMedia = PosicionTablero.y + layout.Alto * 0.5f;
            float z = PosicionTablero.z - 0.04f;

            // A un lado y a otro, dejando libre el hueco del motor y el del PLC.
            float separacion = layout.Ancho * 0.5f + 0.42f + (escala - 1f) * 0.25f;

            var guia = VdfPanelGuia.Construir(
                new Vector3(centroX - separacion, alturaMedia, z),
                rotacionY: -24f,
                ancho: layout.Ancho * 0.80f,
                alto: layout.Alto * 0.80f);

            if (guia != null && guia.GetComponent<VdfPistaObjetivo>() == null)
                guia.gameObject.AddComponent<VdfPistaObjetivo>();

            float anchoMenu = layout.Ancho * 0.62f;
            float anchoDiagnostico = layout.Ancho * 0.80f;
            var centroMenu = new Vector3(centroX + separacion, alturaMedia, z);
            var derechaMenu = Quaternion.Euler(0f, 24f, 0f) * Vector3.right;
            var centroDiagnostico = centroMenu + derechaMenu *
                ((anchoMenu + anchoDiagnostico) * 0.5f * escala + 0.05f);
            var alumno = new Vector3(centroX, alturaMedia, ZDelAlumno);
            float giroDiagnostico = Mathf.Atan2(centroDiagnostico.x - alumno.x,
                                                centroDiagnostico.z - alumno.z) * Mathf.Rad2Deg;
            VdfPanelDiagnostico.Construir(centroDiagnostico, rotacionY: giroDiagnostico,
                                          ancho: anchoDiagnostico);

            var menu = VdfMenuPracticas.Construir(
                centroMenu,
                VdfPracticasDefaults.CrearTodas(),
                rotacionY: 24f,
                // Sin alto: lo calcula el menú según cuántas prácticas haya, que desde la
                // fase 7 son cinco y con un alto fijo el último botón se comía la descripción.
                ancho: layout.Ancho * 0.62f);

            if (escala != 1f)
            {
                foreach (var nombre in new[] { VdfPanelGuia.NombreRaiz, VdfMenuPracticas.NombreRaiz, VdfPanelDiagnostico.NombreRaiz })
                {
                    var go = GameObject.Find(nombre);
                    if (go == null) continue;
                    go.transform.localScale = Vector3.one * escala;
                    ApoyarSobreEncimera(go, nombre == VdfMenuPracticas.NombreRaiz ? 0.17f : 0.04f);
                }
            }

            // En escritorio el menú no se escala, pero la caja de interruptores también lo tapaba.
            if (escala == 1f && menu != null)
            {
                // Y algo más a la derecha, con el panel de diagnóstico que va a su lado: la
                // caja de interruptores, encima del PLC, le tapaba la práctica 5.
                ApoyarSobreEncimera(menu.gameObject, 0.17f);
                menu.transform.position += Vector3.right * 0.08f;
                var diagnostico = GameObject.Find(VdfPanelDiagnostico.NombreRaiz);
                if (diagnostico != null) diagnostico.transform.position += Vector3.right * 0.08f;
            }
            if (escala == 1f && guia != null) ApoyarSobreEncimera(guia.gameObject, 0.17f);

            return "atriles: guía " + (layout.Ancho * 80f).ToString("0") + " x "
                   + (layout.Alto * 80f).ToString("0") + " cm a la izquierda, menú a la derecha"
                   + (guia != null && menu != null ? "" : "  (ATENCION: alguno no se creó)");
        }

        /// <summary>
        /// Cuelga el diagrama Ladder encima del tablero, como el cartel de un aula.
        ///
        /// A su tamaño natural mide 84 x 59 cm, más que el tablero entero, porque se dibujó
        /// cuando el tablero medía 110 x 85. Se reduce para que su ancho quede algo mayor que
        /// el del tablero —se lee de un vistazo sin tapar el trabajo— y se sube por encima,
        /// que es donde no estorba: el diagrama es de solo lectura y se mira mientras se opera.
        /// </summary>
        public static string ColocarLadder()
        {
            var layout = VdfPanelLayoutDefaults.Crear();
            float centroX = PosicionTablero.x + layout.Ancho * 0.5f;
            float arribaTablero = PosicionTablero.y + layout.Alto;

            // El ancho natural del panel es 0.84; se escala al ancho que se quiere ocupar.
            float anchoQuerido = layout.Ancho * 1.10f;
            float escala = anchoQuerido / 0.84f;

            var peldanos = VdfLadderPrograma.Crear();
            float altoNatural = 0.060f + peldanos.Count * 0.050f + 0.030f;
            float alto = altoNatural * escala;

            var panel = VdfLadderPanel.Construir(
                new Vector3(centroX, arribaTablero + alto * 0.5f + 0.05f, PosicionTablero.z - 0.02f),
                rotacionY: 0f,
                ancho: 0.84f,
                // Sin patas: el panel va colgado sobre el tablero, y las patas que trae por
                // defecto bajan hasta el suelo. Colgado y con patas, cruzaban por delante del
                // tablero de punta a punta.
                conPie: false,
                escala: escala);

            return "Ladder: " + peldanos.Count + " peldaños, "
                   + (anchoQuerido * 100f).ToString("0") + " x " + (alto * 100f).ToString("0")
                   + " cm colgado sobre el tablero"
                   + (panel != null ? "" : "  (ATENCION: no se creó)");
        }

        /// <summary>
        /// Encuadra la cámara de escritorio sobre TODOS los bornes, no solo sobre el tablero.
        ///
        /// En escritorio la cámara es fija: no hay órbita ni desplazamiento, así que lo que no
        /// entra en el cuadro no se puede usar. Encuadrando solo el tablero se quedaban fuera
        /// los diez bornes del PLC y del motor, que son precisamente los que hay que conectar
        /// en la práctica de cableado: el alumno veía el enunciado pedirle que llevara Q0.0 a
        /// S1 y no tenía a Q0.0 en pantalla.
        /// </summary>
        public static string EncuadrarCamaraEscritorio()
        {
            var cam = Camera.main;
            if (cam == null) return "no hay cámara principal que encuadrar";

            var bornes = Object.FindObjectsByType<VdfTerminal>();
            if (bornes.Length == 0) return "no hay bornes que encuadrar";

            var caja = new Bounds(bornes[0].transform.position, Vector3.zero);
            for (int i = 1; i < bornes.Length; i++) caja.Encapsulate(bornes[i].transform.position);

            foreach (var nombre in new[] { VdfPanelGuia.NombreRaiz, VdfMenuPracticas.NombreRaiz })
            {
                var go = GameObject.Find(nombre);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    if (r.enabled) caja.Encapsulate(r.bounds);
            }

            cam.nearClipPlane = 0.01f;
            // El componente de la órbita está guardado en la escena con sus valores: sin
            // esto, cambiar el mínimo en el código no llegaba a la escena.
            var orbita = cam.GetComponent<VdfCamaraOrbital>();
            if (orbita != null) orbita.DistanciaMinima = VdfCamaraOrbital.DistanciaMinimaPorDefecto;

            // El aspecto de la cámara en el editor es el de la ventana de Juego, que cambia de
            // tamaño; se encuadra contra 16:9, que es el del instalable.
            float mitadVertical = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
            float mitadHorizontal = Mathf.Atan(Mathf.Tan(mitadVertical) * (16f / 9f));
            float margen = 1.12f;
            float distancia = Mathf.Max(
                (caja.size.x * 0.5f * margen) / Mathf.Tan(mitadHorizontal),
                (caja.size.y * 0.5f * margen) / Mathf.Tan(mitadVertical));

            // Un poco desde arriba, como se mira un banco de trabajo: de frente y a la altura
            // de la mesa, la caja de interruptores tapaba el menú y el motor la guía.
            const float elevacion = 24f * Mathf.Deg2Rad;
            Vector3 ojo = caja.center + new Vector3(0f, Mathf.Sin(elevacion), -Mathf.Cos(elevacion))
                          * (distancia + caja.extents.z);
            cam.transform.position = ojo;
            cam.transform.rotation = Quaternion.LookRotation((caja.center - ojo).normalized, Vector3.up);
            if (orbita != null) orbita.Objetivo = caja.center;

            return "cámara de escritorio a " + distancia.ToString("0.00") + " m, con los "
                   + bornes.Length + " bornes dentro del cuadro";
        }

        /// <summary>Los cables de la pasada anterior, que conservarían la medida vieja.</summary>
        private static void BorrarCablesViejos()
        {
            var cables = Object.FindObjectsByType<VdfXrCable>();
            for (int i = 0; i < cables.Length; i++) Matar(cables[i].gameObject);
        }

        private static Transform BuscarEnTablero(Transform raiz, string nombre)
        {
            var hijos = raiz.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < hijos.Length; i++)
                if (hijos[i].name == nombre) return hijos[i];
            return null;
        }

        private static void Borrar(string nombre)
        {
            var go = GameObject.Find(nombre);
            if (go != null) Matar(go);
        }

        /// <summary>
        /// En Play Mode, Destroy no borra hasta el final del fotograma, así que un objeto
        /// recién «borrado» sigue apareciendo si se mira en la misma llamada. Costó una captura
        /// en la que lo eliminado seguía estando.
        /// </summary>
        private static void Matar(GameObject go)
        {
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }
#endif
    }
}
