// NOTA: un MonoBehaviour por archivo. Ver E6 en VIABILIDAD.md.

using UnityEngine;
using UnityEngine.InputSystem;

namespace VDF
{
    /// <summary>
    /// Orquestador de la práctica completa: une variador, PLC, cableado, teclado,
    /// indicadores y motor, y los hace avanzar en el tiempo.
    ///
    /// Es la única clase que conoce a todas las demás. Cada pieza por separado es C# puro
    /// y está probada aparte; aquí solo se conectan y se les da un reloj.
    /// </summary>
    public class VdfSimulacion : MonoBehaviour
    {
        [Header("Referencias (se buscan solas si se dejan vacías)")]
        public VfdKeypadView Operador;
        public VdfJumperController Cableado;

        [Tooltip("Cableado de VR. Se usa cuando la escena no lleva el de ratón.")]
        public VdfXrWiring CableadoXr;

        [Header("Opciones de arranque")]
        [Tooltip("Aplica la configuración correcta del variador al empezar (salta la práctica 2).")]
        public bool SaltarConfiguracion = true;

        [Tooltip("Cablea correctamente al empezar (salta la práctica 3).")]
        public bool SaltarCableado = true;

        [Tooltip("Exigir que el cableado sea correcto para que el tablero tenga tensión.")]
        public bool ExigirCableadoCorrecto = true;

        [Header("Mando")]
        [Tooltip("Con qué se opera el módulo: los pulsadores del tablero o los tres " +
                 "interruptores encima del PLC (S4, S5 y S6). Los dos modos conviven; " +
                 "este campo solo elige cuál se usa. Ver Docs/Plan_interruptores_practica4.md.")]
        public VdfModoMando ModoMando = VdfModoMando.Pulsadores;

        public static VdfModoMando? ModoMandoForzado;

        /// <summary>Nombre del objeto que agrupa los tres interruptores en la escena.</summary>
        public const string NombreInterruptores = "VDF_Interruptores";

        // --- Modelos -----------------------------------------------------------------------
        public VfdModel Vfd { get; private set; }
        public PlcFanLogic Plc { get; private set; }

        // --- Escena ------------------------------------------------------------------------
        private Transform _ejeMotor;
        private readonly Renderer[] _pilotosV = new Renderer[4];
        private Renderer _pilotoFunc;
        private Renderer _pilotoFalla;

        private Renderer _pilotoE;
        private MaterialPropertyBlock _bloque;

        /// <summary>Último informe de validación del cableado.</summary>
        public VdfWiringReport InformeCableado { get; private set; }

        /// <summary>
        /// Qué señales llegan de verdad con el cableado actual.
        ///
        /// Arranca con todo cableado para que un componente que se consulte antes del primer
        /// <c>RevisarCableado</c> no vea el módulo muerto.
        /// </summary>
        /// <remarks>
        /// NO se serializa, y es a proposito. Unity avisaba de que se saltaba este campo
        /// porque <see cref="VdfPuertaCableado"/> es un struct sin [Serializable]. Marcarlo
        /// serializable seria PEOR: la escena guardaria un estado de cableado viejo que
        /// pisaria al inicializador. Asi arranca siempre en Todo() y lo recalcula
        /// RevisarCableado en cuanto cambia una union.
        /// </remarks>
        [System.NonSerialized]
        public VdfPuertaCableado Puerta = VdfPuertaCableado.Todo();

        /// <summary>
        /// El cableado que manda en esta escena, venga del ratón o del visor.
        ///
        /// En escritorio hay <see cref="VdfJumperController"/> y en VR hay
        /// <see cref="VdfXrWiring"/>, y antes esta clase solo miraba al primero: en la escena de
        /// VR se quedaba en null y la práctica de cableado no tenía consecuencia ninguna.
        /// </summary>
        public IVdfFuenteCableado Cables
        {
            get
            {
                // El de VR manda cuando existe: una escena con equipo de visor puede llevar
                // además el controlador de ratón (la de VR lo lleva desde la fase 4), y si
                // ganaba el de ratón, lo que el alumno enchufaba con las manos iba a un grafo
                // que nadie miraba. Al revés no hay riesgo: la escena de escritorio no tiene
                // puente de VR.
                if (CableadoXr != null) return CableadoXr;
                return Cableado;
            }
        }

        /// <summary>Grados girados por el eje, para que el giro se vea.</summary>
        private float _anguloEje;

        private void Awake()
        {
            Despertar();
        }

        /// <summary>
        /// Crea los modelos y localiza las piezas de la escena.
        ///
        /// Es público y separado de Awake porque **en Edit Mode, añadir un componente por
        /// código no dispara Awake**, así que `Vfd` y `Plc` llegaban a null y la simulación
        /// no se podía usar ni probar fuera de Play Mode. Es la misma lección que llevó a
        /// crear el grafo perezosamente en VdfJumperController: un componente tiene que
        /// poder ponerse en marcha solo.
        ///
        /// Llamarlo dos veces es seguro: no rehace lo que ya existe.
        /// </summary>
        public void Despertar()
        {
            if (Vfd != null && Plc != null) return;

            if (Operador == null) Operador = Object.FindAnyObjectByType<VfdKeypadView>();
            if (Cableado == null) Cableado = Object.FindAnyObjectByType<VdfJumperController>();
            if (CableadoXr == null) CableadoXr = Object.FindAnyObjectByType<VdfXrWiring>();

            // OJO con el orden de Awake. Al principio hacía `Vfd = Operador.Vfd`, y fallaba:
            // el Awake de esta clase corre ANTES que el de VfdKeypadView, así que el
            // operador todavía no había creado su modelo y Vfd quedaba en null.
            //
            // La dependencia va al revés: la simulación es dueña del modelo y se lo entrega
            // al operador. VfdKeypadView.Awake solo crea uno propio si no se lo han dado,
            // así que funciona en cualquier orden de ejecución.
            Vfd = new VfdModel();
            if (Operador != null) Operador.Conectar(Vfd);

            Plc = new PlcFanLogic();

            _bloque = new MaterialPropertyBlock();
            LocalizarPiezasDeEscena();
            if (ModoMandoForzado.HasValue) ModoMando = ModoMandoForzado.Value;
            AplicarModoMando();
        }

        /// <summary>
        /// Pone el PLC en el modo elegido y enseña los interruptores solo si se usan.
        ///
        /// Los interruptores existen siempre en la escena y aquí solo se encienden o se
        /// apagan, para poder cambiar de modo sin reconstruirla. Apagados no estorban: no se
        /// ven, no se señalan y no entran en el grupo de mandos de la práctica 1.
        /// </summary>
        public void AplicarModoMando()
        {
            // El modo arrastra la práctica del laboratorio entera: el cableado correcto, la
            // configuración de referencia del variador y las averías dependen de él.
            bool cambia = VdfMando.Activo != ModoMando;
            VdfMando.Activo = ModoMando;
            if (Plc != null) Plc.CambiarModo(ModoMando);
            if (cambia && Vfd != null && Cables != null) RevisarCableado();

            // Solo en Play: en el editor la caja se queda a la vista para poder retocarla, y
            // así los tests de EditMode no ensucian la escena abierta al despertar una
            // simulación de prueba.
            if (!Application.isPlaying) return;
            var caja = BuscarInterruptores();
            if (caja == null) return;

            // Se les da la simulación antes de apagarlos: un objeto apagado no corre su
            // Awake, que es donde un pulsador la busca, y al encenderlos más tarde no
            // sabrían a quién avisar.
            foreach (var mando in caja.GetComponentsInChildren<VdfPulsadorTablero>(true))
                mando.Simulacion = this;
            caja.SetActive(ModoMando == VdfModoMando.Interruptores);
        }

        /// <summary>Cambia el modo en caliente. Para el menú de pruebas y para los tests.</summary>
        public void CambiarModoMando(VdfModoMando modo)
        {
            bool cambia = modo != ModoMando;
            ModoMando = modo;
            AplicarModoMando();

            // Cambiar de modo es cambiar de práctica de laboratorio: otro cableado y otra
            // configuración del variador. Se vuelve al punto de partida para que el módulo
            // quede cableado y configurado como pide el modo nuevo, no como el viejo.
            if (cambia && Application.isPlaying && Vfd != null) AplicarPuntoDePartida();
        }

        private GameObject _interruptores;

        /// <summary>La caja de interruptores, aunque esté apagada (GameObject.Find no ve los inactivos).</summary>
        private GameObject BuscarInterruptores()
        {
            if (_interruptores != null) return _interruptores;
            var todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < todos.Length; i++)
                if (todos[i].name == NombreInterruptores && todos[i].parent == null)
                    return _interruptores = todos[i].gameObject;
            return null;
        }

        private void Start()
        {
            // En VR el punto de partida se aplica UN FRAME MAS TARDE, y no es un capricho.
            //
            // Cablear el modulo consiste en poner cada punta encima de su borne y marcarla
            // como conectada. En el visor, quien decide de verdad que punta esta en que
            // borne son los sockets de XR, y esos todavia no se han enterado de nada durante
            // el Start: se enganchan en el primer frame. Resultado: el cableado automatico
            // dejaba las puntas colocadas, los sockets despertaban despues y una acababa
            // capturada por el borne vecino.
            //
            // Se veia asi al cargar la escena de VR: diecinueve conexiones pero una mal,
            // BAJAR-A en vez de S/L2, lo que ademas partia en dos la fase L2 y dejaba cinco
            // uniones sin efecto. Y lo delataba que el MISMO metodo, llamado un segundo mas
            // tarde, dejaba el cableado perfecto.
            //
            // Esperando un frame los sockets ya estan vivos y se enganchan donde toca.
            // El mando, ajustado a este simulador: los dos gatillos pulsan, sin «imán» en el
            // agarre y sin deslizar al jugador. Ver VdfAjustesMando.
            if (CableadoXr != null) VdfAjustesMando.Aplicar();

            if (CableadoXr != null) StartCoroutine(AplicarPuntoDePartidaCuandoXrEsteListo());
            else AplicarPuntoDePartida();

            if (Cables != null) Cables.CableadoCambiado += RevisarCableado;
        }

        private System.Collections.IEnumerator AplicarPuntoDePartidaCuandoXrEsteListo()
        {
            yield return null;
            AplicarPuntoDePartida();
        }

        /// <summary>
        /// Deja el módulo como lo pide la práctica que va a empezar: configurado o de fábrica,
        /// cableado o con los bornes limpios.
        ///
        /// Antes esto vivía dentro del Start, y ahí solo servía para el arranque: al elegir
        /// una práctica en el menú, el runner cambiaba SaltarConfiguracion y SaltarCableado
        /// pero ya no había ningún Start que los leyera. El efecto era que la práctica 2
        /// empezaba con el variador ya configurado —es decir, con los pasos resueltos antes de
        /// empezar— y la 3 con el módulo ya cableado.
        /// </summary>
        public void AplicarPuntoDePartida()
        {
            if (Vfd == null) return;

            if (SaltarConfiguracion)
            {
                Vfd.EscribirParametro("A1-03", 2220);
                Vfd.AplicarConfiguracionDeReferencia();
            }
            else
            {
                // La práctica 2 empieza desde fábrica de verdad: si no se restaura, el alumno
                // se encuentra el variador ya configurado y no hay nada que programar.
                Vfd.RestaurarValoresDeFabrica();
            }

            if (Plc != null) Plc.Reiniciar();

            // El operador del variador tambien vuelve a reposo. Su estado —que menu esta
            // abierto, que parametro se esta editando y con que digitos— no vive en VfdModel,
            // asi que sobrevivia al cambio de practica: el alumno dejaba un valor a medias,
            // abria otra practica y se encontraba el teclado en modo edicion con los digitos
            // de antes. Ademas el variador sale de LOCAL, porque una practica nueva no puede
            // empezar con los terminales mudos por una tecla que se pulso en la anterior.
            if (Operador != null && Operador.Teclado != null) Operador.Teclado.VolverAReposo();
            Vfd.ModoLocal = false;

            // Ojo con el orden y con la exclusividad: los dos cableados COMPARTEN el grafo.
            //
            // Antes se ejecutaban las dos ramas, asi que en la escena de VR el modulo quedaba
            // cableado dos veces, una por cada via. El efecto era grave y silencioso: como
            // SoltarTodos solo retira los cables que puso el puente de VR, al soltarlos todos
            // quedaban los del raton por debajo, el grafo seguia completo y la validacion
            // decia «correcto» con los veinte cables en la bandeja. Para el alumno con el
            // visor eso es la practica 3 sin ninguna consecuencia: suelta todo y el modulo
            // sigue funcionando.
            //
            // Asi que manda una sola via, y en VR es la de VR. El cableado por raton se
            // vacia siempre, tambien cuando la practica empieza cableada, porque en esa
            // escena quien tiene que poner los cables es el puente del visor.
            bool mandaVr = CableadoXr != null;

            // Si la practica empieza cableada y el modulo YA esta bien cableado, no se toca.
            //
            // Antes se borraba todo y se volvia a tender siempre. Para el alumno eso
            // significa que, al pasar de la practica de cableado a la de operacion, su
            // trabajo recien hecho desaparece y se rehace solo delante de el. Y en el video
            // se veia como un parpadeo: los cables saltando entre practica y practica sin
            // que pasara nada que lo justificara.
            //
            // Que desaparezcan al ENTRAR en la practica de cableado si tiene sentido, porque
            // es lo que se va a hacer, y eso se mantiene.
            bool yaCorrecto = SaltarCableado && Cables != null && Cables.Validar().EsCorrecto;

            if (Cableado != null && !yaCorrecto)
            {
                Cableado.BorrarTodo();
                if (SaltarCableado && !mandaVr) Cableado.CablearCorrectamente();
            }

            // En VR «dejar el módulo sin cablear» no es vaciar una lista: hay que devolver los
            // veinte cables a la bandeja, porque son objetos que están puestos en el tablero.
            if (mandaVr && !yaCorrecto)
            {
                if (SaltarCableado) CableadoXr.EnchufarCableadoCorrecto();
                else CableadoXr.SoltarTodos();
            }

            RevisarCableado();
        }

        private void OnDestroy()
        {
            if (Cables != null) Cables.CableadoCambiado -= RevisarCableado;
        }

        /// <summary>Recalcula el informe y decide si el tablero tiene tensión.</summary>
        public void RevisarCableado()
        {
            var cables = Cables;
            if (cables == null)
            {
                InformeCableado = null;
                return;
            }

            InformeCableado = cables.Validar();

            if (!ExigirCableadoCorrecto)
            {
                Puerta = VdfPuertaCableado.Todo();
                Vfd.SetAlimentacion(true);
                return;
            }

            // Qué llega de verdad con este cableado: tensión, común de entradas, S1/S5/S6,
            // los cuatro pilotos y las tres fases del motor. Se calcula aquí, una vez por
            // cambio de cableado, y no en el Update.
            Puerta = VdfPuertaCableado.Calcular(cables.Grafo);
            Vfd.SetAlimentacion(Puerta.Alimentado);
        }

        // =====================================================================================
        // Bucle
        // =====================================================================================

        private void Update()
        {
            // Si los modelos no están, no hay nada que hacer. Pasa al recompilar con el editor
            // en Play: el dominio se recarga, los modelos no son serializables y se pierden,
            // y el Update seguía corriendo y soltando una excepción por fotograma.
            if (Plc == null || Vfd == null) return;

            LeerTeclado();
            LeerPulsadores();

            float dt = Time.deltaTime;

            Plc.Tick(dt);
            Plc.AplicarA(Vfd, Puerta);
            Vfd.Tick(dt);

            GirarMotor(dt);
            PintarIndicadores();
        }

        /// <summary>
        /// Acciona con el ratón el pulsador que haya bajo el cursor.
        ///
        /// Los atajos de teclado se quedan: son cómodos para probar y para quien no quiera
        /// apuntar. Pero el alumno espera poder pulsar START en el tablero, y hasta ahora eso
        /// no hacía nada.
        /// </summary>
        private void LeerPulsadores()
        {
            var raton = Mouse.current;
            if (raton == null || !raton.leftButton.wasPressedThisFrame) return;

            var cam = Camera.main;
            if (cam == null) return;

            RaycastHit hit;
            var rayo = cam.ScreenPointToRay(raton.position.ReadValue());
            if (!Physics.Raycast(rayo, out hit, 50f, ~0, QueryTriggerInteraction.Collide)) return;

            var mando = hit.collider.GetComponentInParent<VdfPulsadorTablero>();
            if (mando != null) mando.Pulsar();
        }

        /// <summary>
        /// Pulsadores del tablero por teclado, para poder hacer la práctica en escritorio.
        /// En VR los mismos métodos los llamará el pulsador físico.
        /// </summary>
        private void LeerTeclado()
        {
            var t = Keyboard.current;
            if (t == null) return;

            if (t.qKey.wasPressedThisFrame) PulsarStart();
            if (t.wKey.wasPressedThisFrame) PulsarStop();
            if (t.upArrowKey.wasPressedThisFrame) PulsarSubir();
            if (t.downArrowKey.wasPressedThisFrame) PulsarBajar();
            if (t.spaceKey.wasPressedThisFrame) PulsarEmergencia();
            if (t.rKey.wasPressedThisFrame) Rearmar();

            if (ModoMando == VdfModoMando.Interruptores)
            {
                if (t.aKey.wasPressedThisFrame) ConmutarInterruptor(VdfInterruptor.Marcha);
                if (t.sKey.wasPressedThisFrame) ConmutarInterruptor(VdfInterruptor.S5);
                if (t.dKey.wasPressedThisFrame) ConmutarInterruptor(VdfInterruptor.S6);
            }
        }

        // --- API de los pulsadores del tablero ----------------------------------------------
        /// <summary>
        /// Salta con cada pulsación, venga del ratón, del teclado o de la mano en VR.
        ///
        /// Lo escucha el diagrama Ladder para hacer destellar el contacto del pulsador. Va por
        /// evento y no por llamada directa a propósito: el interesado es el diagrama, que es
        /// opcional y puede no estar en la escena, así que no tiene sentido que la simulación
        /// dependa de él. Antes nadie avisaba y los contactos de START, STOP, SUBIR y BAJAR no
        /// se encendían nunca; los tests no lo vieron porque avisaban ellos mismos.
        /// </summary>
        public event System.Action<VdfAccionPulsador> Pulsado;

        private void Anunciar(VdfAccionPulsador accion)
        {
            var h = Pulsado;
            if (h != null) h(accion);
        }

        public void PulsarStart() { Plc.PulsarStart(); Anunciar(VdfAccionPulsador.Start); }
        public void PulsarStop() { Plc.PulsarStop(); Anunciar(VdfAccionPulsador.Stop); }
        public void PulsarSubir() { Plc.PulsarSubir(); Anunciar(VdfAccionPulsador.Subir); }
        public void PulsarBajar() { Plc.PulsarBajar(); Anunciar(VdfAccionPulsador.Bajar); }

        /// <summary>Cambia de posición un interruptor, venga del ratón, del teclado o del visor.</summary>
        public void ConmutarInterruptor(VdfInterruptor cual)
        {
            Plc.ConmutarInterruptor(cual);
            Anunciar(AccionDe(cual));
        }

        /// <summary>Pone un interruptor arriba o abajo.</summary>
        public void FijarInterruptor(VdfInterruptor cual, bool arriba)
        {
            if (Plc.Interruptor(cual) == arriba) return;
            Plc.FijarInterruptor(cual, arriba);
            Anunciar(AccionDe(cual));
        }

        private static VdfAccionPulsador AccionDe(VdfInterruptor cual)
        {
            if (cual == VdfInterruptor.S5) return VdfAccionPulsador.InterruptorS5;
            if (cual == VdfInterruptor.S6) return VdfAccionPulsador.InterruptorS6;
            return VdfAccionPulsador.InterruptorMarcha;
        }

        public void PulsarEmergencia()
        {
            Plc.PulsarEmergencia();
            Plc.AplicarA(Vfd, Puerta);
            Anunciar(VdfAccionPulsador.Emergencia);
        }

        public void Rearmar()
        {
            Plc.RearmarEmergencia();
            Vfd.ResetFalla();
        }

        // =====================================================================================
        // Escena
        // =====================================================================================

        private void LocalizarPiezasDeEscena()
        {
            var motor = GameObject.Find(VdfPanelBuilder.NombreMotor);
            if (motor != null)
            {
                var eje = motor.transform.Find("Eje");
                if (eje != null) _ejeMotor = eje;
            }

            var bornes = Object.FindObjectsByType<VdfTerminal>();
            // Los pilotos no son bornes; se buscan por nombre de nodo del builder.
            _pilotoFunc = BuscarPiloto("PilotoLed_FUNC");
            _pilotoFalla = BuscarPiloto("PilotoLed_FALLA");
            _pilotoE = BuscarPiloto("PilotoLed_E");
            for (int i = 0; i < 4; i++) _pilotosV[i] = BuscarPiloto("PilotoLed_V" + (i + 1));
        }

        private static Renderer BuscarPiloto(string nombreNodo)
        {
            var go = GameObject.Find(nombreNodo);
            if (go == null) return null;
            return go.GetComponentInChildren<Renderer>();
        }

        /// <summary>
        /// Gira el eje a velocidad proporcional a la frecuencia de salida.
        /// 60 Hz con 2 pares de polos ≈ 1800 rpm; se escala para que se vea sin estroboscopio.
        /// </summary>
        private void GirarMotor(float dt)
        {
            if (_ejeMotor == null) return;

            // Sin las tres fases cableadas el eje no gira, aunque el variador este dando
            // frecuencia. Antes giraba igual, y eso quitaba consecuencias a la parte de la
            // practica 3 que conecta U, V y W.
            if (!Puerta.Motor) return;

            float rpm = Vfd.FrecuenciaSalidaHz * 30f;        // 60 Hz -> 1800 rpm
            float gradosPorSegundo = rpm * 6f * 0.08f;       // escalado para que el ojo lo siga
            if (Vfd.Reversa) gradosPorSegundo = -gradosPorSegundo;

            _anguloEje += gradosPorSegundo * dt;
            _ejeMotor.localRotation = Quaternion.Euler(_anguloEje, 0f, 0f);
        }

        private void PintarIndicadores()
        {
            // Un piloto se enciende si el PLC lo manda Y su cable está puesto. Sin la segunda
            // mitad, la falla V3_SIN_CABLE de la práctica 5 no tenía síntoma: se quitaba el
            // cable y el piloto seguía encendiéndose igual.
            for (int i = 0; i < 4; i++)
                Encender(_pilotosV[i], Plc.SalidaV(i + 1) && Puerta.Piloto(i + 1),
                    new Color(0.25f, 1f, 0.35f));

            Encender(_pilotoFunc, Vfd.Estado == VfdState.EnMarcha || Vfd.Estado == VfdState.Acelerando,
                new Color(0.25f, 1f, 0.35f));
            // El piloto de FALLA del módulo real es AZUL. Estaba en rojo por lo que parecía
            // de sentido común, pero la foto del tablero lo desmiente: el que montaron es azul.
            Encender(_pilotoFalla, Vfd.Falla != VfdFault.Ninguna, new Color(0.25f, 0.5f, 1f));
            // Azul, no ambar: la lente del modulo real es azul, igual que la de FALLA. Una
            // lente azul encendiendose en ambar no da ese color, da verde sucio.
            Encender(_pilotoE, Plc.EmergenciaActiva, new Color(0.30f, 0.55f, 1f));
        }

        private void Encender(Renderer r, bool encendido, Color color)
        {
            if (r == null) return;

            r.GetPropertyBlock(_bloque);
            Color c = encendido ? color : color * 0.25f;
            _bloque.SetColor("_BaseColor", c);
            _bloque.SetColor("_Color", c);
            _bloque.SetColor("_EmissionColor", encendido ? color * 0.6f : Color.black);
            r.SetPropertyBlock(_bloque);
        }

        // =====================================================================================
        // Estado para la guía en pantalla
        // =====================================================================================

        /// <summary>Resumen de una línea del estado del sistema.</summary>
        public string Resumen()
        {
            if (Vfd == null || Plc == null) return "simulacion no inicializada";

            string alim = Vfd.Alimentado ? "con tension" : "SIN TENSION";
            string falla = Vfd.Falla == VfdFault.Ninguna ? "" : "  FALLA: " + Vfd.Falla;
            return "Variador: " + Vfd.Estado + " @ " + Vfd.FrecuenciaSalidaHz.ToString("00.0") + " Hz (" + alim + ")"
                 + "   PLC: " + (Plc.Habilitado ? "habilitado" : "en reposo")
                 + "  velocidad " + Plc.NivelActivo + "/4"
                 + (Plc.CambioEnCurso ? "  (cambiando a " + Plc.NivelPendiente + " en " + Plc.TiempoRestanteCambio.ToString("0.0") + " s)" : "")
                 + falla;
        }
    }
}
