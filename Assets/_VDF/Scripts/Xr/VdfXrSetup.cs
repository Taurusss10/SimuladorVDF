using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    /// <summary>
    /// Monta la capa XR sobre una escena que ya tenga el tablero construido:
    /// sockets en todos los bornes, cables agarrables y teclas pulsables con el dedo.
    ///
    /// Se ejecuta desde el editor (o desde un script de arranque), no en cada Play.
    /// </summary>
    public static class VdfXrSetup
    {
        /// <summary>
        /// Radio del socket. Generoso a propósito, porque encajar en VR con precisión milimétrica
        /// es frustrante, pero no más que la mitad del paso entre bornes: al pasar el tablero a
        /// las medidas de catálogo los bornes quedaron a 18 mm, y con el radio anterior (30 mm) un
        /// socket se tragaba también a sus dos vecinos, así que en el visor no había forma de
        /// cablear el borne que se quería.
        /// </summary>
        /// <summary>
        /// Radio del socket de un borne.
        ///
        /// Este numero y el del collider de la punta NO son independientes: un socket captura
        /// una punta cuando los dos volumenes se tocan, asi que el alcance real de captura es
        /// la SUMA de los dos radios. Para que una punta encima de un borne no pueda ser
        /// capturada por el borne de al lado, esa suma tiene que quedar por debajo de la
        /// separacion minima entre bornes, que en este tablero son 18 mm.
        ///
        ///     7,5 + 7,5 = 15 mm  <  18 mm
        ///
        /// Antes eran 8,5 + 14 = 22,5 mm, o sea que el alcance de captura se comia al vecino.
        /// Ver el comentario del collider de agarre, mas abajo, para lo que eso provocaba.
        /// </summary>
        public const float RadioSocket = 0.0075f;

        /// <summary>Tamaño del conector banana agarrable. Algo mayor que el borne, para verlo al agarrarlo.</summary>
        public const float TamanoConector = 0.012f;

        private static readonly Color[] Paleta =
        {
            new Color(0.85f, 0.15f, 0.13f),
            new Color(0.10f, 0.10f, 0.11f),
            new Color(0.15f, 0.38f, 0.78f),
            new Color(0.16f, 0.65f, 0.25f),
            new Color(0.93f, 0.82f, 0.12f),
            new Color(0.80f, 0.81f, 0.83f)
        };

        /// <summary>
        /// Pone un <see cref="XRSocketInteractor"/> en cada bornera de la escena.
        /// Devuelve cuántos creó.
        /// </summary>
        public static int CrearSockets(VdfXrWiring puente)
        {
            var bornes = Object.FindObjectsByType<VdfTerminal>();
            int creados = 0;

            for (int i = 0; i < bornes.Length; i++)
            {
                var borne = bornes[i];
                if (borne.GetComponentInChildren<VdfXrSocket>() != null) continue;

                var nodo = new GameObject("Socket_" + borne.Id);
                nodo.transform.SetParent(borne.transform, false);
                nodo.transform.localPosition = borne.PuntoConexionLocal;

                var trigger = nodo.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = RadioSocket;

                var socket = nodo.AddComponent<XRSocketInteractor>();
                socket.showInteractableHoverMeshes = false;
                socket.socketActive = true;

                // El punto al que se alinea el conector al encajar.
                var anclaje = new GameObject("Anclaje");
                anclaje.transform.SetParent(nodo.transform, false);
                socket.attachTransform = anclaje.transform;

                var marcador = nodo.AddComponent<VdfXrSocket>();
                marcador.Inicializar(borne, puente, socket);
                creados++;
            }

            return creados;
        }

        /// <summary>
        /// Crea <paramref name="cuantos"/> cables agarrables apilados sobre una bandeja,
        /// para que el alumno los coja de ahí.
        /// </summary>
        public static int CrearCables(VdfXrWiring puente, Vector3 origenBandeja, int cuantos)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var contenedor = new GameObject("VDF_CablesXR");
            int creados = 0;

            for (int i = 0; i < cuantos; i++)
            {
                Color color = Paleta[i % Paleta.Length];

                var mat = new Material(shader) { name = "VDF_CableXR_" + i };
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);

                var cableGo = new GameObject("CableXR_" + i);
                cableGo.transform.SetParent(contenedor.transform, false);
                var cable = cableGo.AddComponent<VdfXrCable>();

                Vector3 baseP = origenBandeja + new Vector3((i % 10) * 0.035f, (i / 10) * 0.03f, 0f);
                var a = CrearConector(cableGo.transform, cable, true, baseP, color);
                var b = CrearConector(cableGo.transform, cable, false, baseP + new Vector3(0f, 0f, -0.14f), color);

                cable.Inicializar(a, b, color, mat);
                puente.Registrar(cable);
                creados++;
            }

            return creados;
        }

        private static VdfXrPlug CrearConector(Transform padre, VdfXrCable cable, bool esA,
                                               Vector3 posicion, Color color)
        {
            var go = new GameObject((esA ? "ExtremoA" : "ExtremoB"));
            go.transform.SetParent(padre, false);
            go.transform.position = posicion;

            var cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cuerpo.name = "Cuerpo";
            cuerpo.transform.SetParent(go.transform, false);
            cuerpo.transform.localScale = new Vector3(TamanoConector, 0.016f, TamanoConector);
            cuerpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var colliderSobrante = cuerpo.GetComponent<Collider>();
            if (colliderSobrante != null)
            {
                if (Application.isPlaying) Object.Destroy(colliderSobrante);
                else Object.DestroyImmediate(colliderSobrante);
            }
            VdfPanelBuilder.PintarPublico(cuerpo, color, 0.3f);

            // Collider de agarre: una esfera comoda, no la geometria exacta del conector.
            //
            // Tenia 14 mm de radio, elegidos para que quedara por debajo de la mitad de la
            // separacion entre conectores DE LA BANDEJA (35 mm), y para eso estaba bien. Lo
            // que nadie comparo es con la separacion entre BORNES, que son 18 mm en lo mas
            // apretado del tablero.
            //
            // Una punta se queda enchufada cuando su esfera toca la del socket, asi que el
            // alcance de captura era 14 + 8,5 = 22,5 mm: una punta puesta encima de un borne
            // invadia el socket del vecino. Y como dos sockets pueden querer la misma punta y
            // gana el que se procese antes, no el mas cercano, el resultado era aleatorio.
            //
            // Se veia al cargar la escena de VR: el cable que debia ir de BRK-L2 a S/L2
            // acababa en BAJAR-A, que esta a 18 mm de S/L2. Eso partia en dos la fase L2 y
            // dejaba cinco uniones sin efecto. Pero lo grave no era el arranque: con ese
            // alcance, un alumno que enchufara bien en S/L2 podia quedar conectado a BAJAR-A
            // y ver "mal cableado" habiendolo hecho bien.
            //
            // Con 7,5 mm el alcance baja a 15 mm y ningun borne llega al vecino. Sigue siendo
            // casi el doble de un conector de verdad, y la bandeja (35 mm) no se resiente.
            var agarre = go.AddComponent<SphereCollider>();
            agarre.radius = 0.0075f;

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;

            var plug = go.AddComponent<VdfXrPlug>();
            plug.Cable = cable;
            plug.EsExtremoA = esA;
            plug.Reposo = posicion;
            return plug;
        }

        /// <summary>
        /// Hace que las teclas del operador se puedan pulsar con el dedo.
        /// Devuelve cuántas preparó.
        /// </summary>
        public static int PrepararTeclas()
        {
            var vista = Object.FindAnyObjectByType<VfdKeypadView>();
            if (vista == null) return 0;

            var botones = Object.FindObjectsByType<VfdKeyButton>();
            int preparadas = 0;

            for (int i = 0; i < botones.Length; i++)
            {
                var boton = botones[i];
                HacerAccionable(boton.gameObject);
                if (boton.GetComponent<VdfXrKeyPoke>() != null) continue;

                var interactable = boton.gameObject.GetComponent<XRSimpleInteractable>();
                if (interactable == null) interactable = boton.gameObject.AddComponent<XRSimpleInteractable>();

                // El collider de la tecla ya existe (lo puso VfdKeypadView como trigger).
                //
                // NOTA: deliberadamente NO se añade un XRPokeFilter. Su API real exige un
                // PokeThresholdDatum (dirección de empuje y profundidad de activación), y
                // esos dos números solo se pueden afinar con el visor puesto: dependen de
                // a qué distancia la persona siente que "ha pulsado". Poner valores
                // inventados daría una sensación de falso positivo o de tecla muerta.
                // Sin el filtro, la tecla se activa igualmente al tocarla con el
                // XRPokeInteractor; lo que falta es el umbral fino. Ver E6 en el informe.

                var marcador = boton.gameObject.AddComponent<VdfXrKeyPoke>();
                marcador.Inicializar(vista, boton, interactable);
                preparadas++;
            }

            return preparadas;
        }

        public static bool EstaEnUnSocket(GameObject punta)
        {
            var agarre = punta.GetComponent<XRGrabInteractable>();
            if (agarre == null) return false;
            foreach (var quien in agarre.interactorsSelecting)
                if (quien is XRSocketInteractor) return true;
            return false;
        }

        /// <summary>¿Lo ven el rayo y el toque del mando? Para los tests.</summary>
        public static bool EsAccionable(GameObject destino)
        {
            var col = destino.GetComponent<Collider>();
            var filtro = destino.GetComponent<XRPokeFilter>();
            return col != null && !col.isTrigger && filtro != null
                   && filtro.pokeConfiguration.Value != null
                   && filtro.pokeConfiguration.Value.pokeDirection != PokeAxis.None;
        }

        public static void HacerAccionable(GameObject destino)
        {
            var col = destino.GetComponent<Collider>();
            if (col != null) col.isTrigger = false;

            var filtro = destino.GetComponent<XRPokeFilter>();
            if (filtro == null) filtro = destino.AddComponent<XRPokeFilter>();
            filtro.pokeConfiguration = new PokeThresholdDatumProperty(new PokeThresholdData
            {
                pokeDirection = DireccionDeToque(destino.transform),
                interactionDepthOffset = 0f,
                enablePokeAngleThreshold = false,
            });
        }

        public static PokeAxis DireccionDeToque(Transform t)
        {
            Vector3 alumno = new Vector3(t.position.x, 1.40f, VdfReconstructorEscena.ZDelAlumno);
            Vector3 hacia = (alumno - t.position).normalized;

            var opciones = new[]
            {
                new System.Tuple<Vector3, PokeAxis>(-t.forward, PokeAxis.Z),
                new System.Tuple<Vector3, PokeAxis>(t.forward, PokeAxis.NegativeZ),
                new System.Tuple<Vector3, PokeAxis>(-t.right, PokeAxis.X),
                new System.Tuple<Vector3, PokeAxis>(t.right, PokeAxis.NegativeX),
                new System.Tuple<Vector3, PokeAxis>(-t.up, PokeAxis.Y),
                new System.Tuple<Vector3, PokeAxis>(t.up, PokeAxis.NegativeY),
            };

            PokeAxis mejor = PokeAxis.Z;
            float mejorDot = float.MinValue;
            foreach (var o in opciones)
            {
                float d = Vector3.Dot(o.Item1, hacia);
                if (d > mejorDot) { mejorDot = d; mejor = o.Item2; }
            }
            return mejor;
        }

        /// <summary>
        /// Hace pulsables en VR los botones del menú de prácticas. Devuelve cuántos preparó.
        ///
        /// Sin esto el menú solo funciona con ratón, y en un visor no hay ratón: el alumno
        /// no podía elegir práctica con las gafas puestas.
        /// </summary>
        public static int PrepararBotonesDeMenu()
        {
            // INCLUYENDO LOS INACTIVOS, y no es un detalle. El boton de "listo, entendido"
            // del atril nace oculto -solo aparece en los pasos que hay que confirmar a mano-
            // y sin esto se quedaria sin su puente de XR: en el visor seria el unico boton
            // del simulador que no responde, y justo el que desbloquea siete pasos.
            var botones = Object.FindObjectsByType<VdfBotonPractica>(FindObjectsInactive.Include);
            int preparados = 0;

            for (int i = 0; i < botones.Length; i++)
            {
                var b = botones[i];
                // También con el toque: quien tiene el mando pegado al panel lo intenta así.
                HacerAccionable(b.gameObject);
                if (b.GetComponent<VdfXrBotonMenu>() != null) continue;

                // El collider del botón ya existe: lo puso VdfBotonPractica al construirlo,
                // algo más grueso que la chapa para que acertarle con el rayo sea cómodo.
                var inter = b.gameObject.GetComponent<XRSimpleInteractable>();
                if (inter == null) inter = b.gameObject.AddComponent<XRSimpleInteractable>();

                var puente = b.gameObject.AddComponent<VdfXrBotonMenu>();
                puente.Inicializar(b, inter);
                preparados++;
            }

            return preparados;
        }

        public static int PrepararPulsadoresDelTablero()
        {
            var pulsadores = Object.FindObjectsByType<VdfPulsadorTablero>(FindObjectsInactive.Include);
            int preparados = 0;

            for (int i = 0; i < pulsadores.Length; i++)
            {
                var p = pulsadores[i];
                var yaPuesto = p.GetComponentInChildren<VdfXrPulsador>(true);
                if (yaPuesto != null) { HacerAccionable(yaPuesto.gameObject); continue; }

                // El interactuable va sobre el objeto que TIENE el collider, no sobre la
                // raíz: XRI empareja el interactuable con los colliders de su propio
                // GameObject y de sus hijos, y la cabeza del pulsador cuelga de la raíz.
                var col = p.GetComponentInChildren<Collider>(true);
                if (col == null) continue;

                var destino = col.gameObject;
                HacerAccionable(destino);
                var inter = destino.GetComponent<XRSimpleInteractable>();
                if (inter == null) inter = destino.AddComponent<XRSimpleInteractable>();

                var puente = destino.AddComponent<VdfXrPulsador>();
                puente.Inicializar(p, inter);
                preparados++;
            }

            return preparados;
        }

        /// <summary>
        /// Hace que los bornes se puedan SEÑALAR con el rayo o el dedo. Devuelve cuántos.
        ///
        /// Ojo con la distinción: los bornes ya tenían un XRSocketInteractor, pero eso sirve
        /// para recibir un conector, no para señalarlos. Señalar es lo que pide la práctica
        /// de reconocimiento, y sin esto solo funcionaba con ratón.
        /// </summary>
        public static int PrepararBornesParaSenalar()
        {
            var cableado = Object.FindAnyObjectByType<VdfJumperController>();
            var bornes = Object.FindObjectsByType<VdfTerminal>();
            int preparados = 0;

            for (int i = 0; i < bornes.Length; i++)
            {
                var t = bornes[i];

                var propio = t.GetComponent<Collider>();
                if (propio != null) propio.isTrigger = false;

                if (t.GetComponent<VdfXrSenalarBorne>() != null) continue;

                var inter = t.gameObject.GetComponent<XRSimpleInteractable>();
                if (inter == null) inter = t.gameObject.AddComponent<XRSimpleInteractable>();

                var puente = t.gameObject.AddComponent<VdfXrSenalarBorne>();
                puente.Inicializar(t, cableado, inter);
                preparados++;
            }

            return preparados;
        }

        /// <summary>Resumen de lo montado, para dejar constancia en el informe.</summary>
        public static string Montar(VdfXrWiring puente, Vector3 bandeja, int cables)
        {
            int sockets = CrearSockets(puente);
            int creados = CrearCables(puente, bandeja, cables);
            int teclas = PrepararTeclas();
            int botones = PrepararBotonesDeMenu();
            int pulsadores = PrepararPulsadoresDelTablero();
            int senalables = PrepararBornesParaSenalar();

            return "sockets=" + sockets + "  cables=" + creados + "  teclas=" + teclas +
                   "  botones de menu=" + botones + "  pulsadores=" + pulsadores +
                   "  bornes senalables=" + senalables;
        }
    }
}
