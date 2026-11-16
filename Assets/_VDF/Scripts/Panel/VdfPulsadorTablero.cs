
using UnityEngine;

namespace VDF
{
    /// <summary>Qué hace cada pulsador del tablero cuando se acciona.</summary>
    public enum VdfAccionPulsador
    {
        Start,
        Stop,
        Subir,
        Bajar,
        Emergencia,

        // Los tres interruptores del modo VdfModoMando.Interruptores. Van en este mismo
        // enum para aprovechar todo lo que ya acciona un pulsador: el ratón, el rayo y el
        // dedo en VR, la etiqueta al apuntar y el clic con vibración.
        InterruptorMarcha,
        InterruptorS5,
        InterruptorS6
    }

    public class VdfPulsadorTablero : MonoBehaviour
    {
        private VdfPracticaRunner _runner;

        public VdfAccionPulsador Accion;

        [Tooltip("Si se deja vacío, se busca en la escena al arrancar.")]
        public VdfSimulacion Simulacion;

        [Tooltip("Parte que se hunde al pulsar. Si se deja vacía, se busca sola entre los hijos.")]
        public Transform Capuchon;

        /// <summary>Cuánto se hunde el capuchón, en metros.</summary>
        public float Recorrido = 0.0015f;

        /// <summary>Cuánto dura la animación de hundido, en segundos.</summary>
        public float Duracion = 0.12f;

        private Vector3 _reposo;
        private float _restante;
        private bool _enclavado;
        private bool _tengoReposo;

        /// <summary>¿Es uno de los tres interruptores, y no un pulsador?</summary>
        public bool EsInterruptor { get { return EsAccionDeInterruptor(Accion); } }

        public static bool EsAccionDeInterruptor(VdfAccionPulsador accion)
        {
            return accion == VdfAccionPulsador.InterruptorMarcha
                || accion == VdfAccionPulsador.InterruptorS5
                || accion == VdfAccionPulsador.InterruptorS6;
        }

        /// <summary>Qué interruptor del PLC mueve esta acción.</summary>
        public static VdfInterruptor InterruptorDe(VdfAccionPulsador accion)
        {
            if (accion == VdfAccionPulsador.InterruptorS5) return VdfInterruptor.S5;
            if (accion == VdfAccionPulsador.InterruptorS6) return VdfInterruptor.S6;
            return VdfInterruptor.Marcha;
        }

        /// <summary>Grados que gira la palanca de un interruptor hacia arriba o hacia abajo.</summary>
        public const float GiroPalanca = 30f;

        private Transform _palanca;
        private bool _palancaArriba;
        private bool _palancaPintada;

        private void Awake()
        {
            if (Simulacion == null) Simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            if (Capuchon == null) Capuchon = BuscarCapuchon();
            if (Capuchon != null) { _reposo = Capuchon.localPosition; _tengoReposo = true; }
            if (EsInterruptor) _palanca = transform.Find("Palanca");
        }

        private Transform BuscarCapuchon()
        {
            string[] candidatos = { "Seta", "Capuchon", "Pulsador" };
            var hijos = GetComponentsInChildren<Transform>(true);

            for (int c = 0; c < candidatos.Length; c++)
                for (int i = 0; i < hijos.Length; i++)
                    if (hijos[i] != transform && hijos[i].name.StartsWith(candidatos[c]))
                        return hijos[i];

            return null;
        }

        /// <summary>
        /// Punto de entrada único. Lo llaman el ratón, el dedo y el rayo.
        /// </summary>
        public void Pulsar()
        {
            if (_runner == null) _runner = Object.FindAnyObjectByType<VdfPracticaRunner>();
            if (_runner != null) _runner.Identificar(gameObject.name);

            // En el reconocimiento solo se señala. Ver VdfPractica.PulsadoresSoloSenalan.
            if (_runner != null && _runner.Practica != null && _runner.Practica.PulsadoresSoloSenalan) return;

            if (Simulacion == null) Simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            if (Simulacion == null) return;

            switch (Accion)
            {
                case VdfAccionPulsador.Start: Simulacion.PulsarStart(); break;
                case VdfAccionPulsador.Stop: Simulacion.PulsarStop(); break;
                case VdfAccionPulsador.Subir: Simulacion.PulsarSubir(); break;
                case VdfAccionPulsador.Bajar: Simulacion.PulsarBajar(); break;

                case VdfAccionPulsador.Emergencia:
                    if (Simulacion.Plc != null && Simulacion.Plc.EmergenciaActiva) Simulacion.Rearmar();
                    else Simulacion.PulsarEmergencia();

                    RefrescarEnclavado(Simulacion.Plc != null && Simulacion.Plc.EmergenciaActiva);
                    return;

                case VdfAccionPulsador.InterruptorMarcha:
                case VdfAccionPulsador.InterruptorS5:
                case VdfAccionPulsador.InterruptorS6:
                    Simulacion.ConmutarInterruptor(InterruptorDe(Accion));
                    RefrescarPalanca();
                    return;
            }

            Animar();
        }

        /// <summary>Hunde el capuchón, para que se note que se ha pulsado.</summary>
        public void Animar()
        {
            if (!_tengoReposo || Capuchon == null) return;

            Hundir(true);
            _restante = Duracion;
        }

        /// <summary>
        /// Deja pasar el tiempo y devuelve el capuchón a su sitio cuando toca.
        ///
        /// Es público y con el tiempo por parámetro, como el Tick del PLC: si el retorno
        /// viviera dentro de Update y midiera con Time.time, no habría forma de comprobarlo
        /// sin entrar en Play Mode. De hecho el primer test que se escribió fallaba justo por
        /// eso, y el fallo era del diseño, no del test.
        /// </summary>
        public void Avanzar(float dt)
        {
            if (!_tengoReposo || Capuchon == null) return;
            if (_enclavado || _restante <= 0f) return;

            _restante -= dt;
            if (_restante <= 0f)
            {
                _restante = 0f;
                Hundir(false);
            }
        }

        private void Update()
        {
            Avanzar(Time.deltaTime);
            if (EsInterruptor) RefrescarPalanca();
        }

        /// <summary>
        /// Pone la palanca como esté el interruptor en el PLC. Se lee del estado en vez de
        /// recordarlo aquí, porque hay otras cosas que lo cambian: reiniciar la práctica los
        /// baja todos, y en escritorio también se mueven con el teclado.
        /// </summary>
        public void RefrescarPalanca()
        {
            if (_palanca == null) _palanca = transform.Find("Palanca");
            if (_palanca == null) return;
            if (Simulacion == null) Simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            if (Simulacion == null || Simulacion.Plc == null) return;

            bool arriba = Simulacion.Plc.Interruptor(InterruptorDe(Accion));
            if (_palancaPintada && arriba == _palancaArriba) return;

            _palancaArriba = arriba;
            _palancaPintada = true;
            // La palanca sale hacia -Z (hacia el alumno): girar +X la levanta.
            _palanca.localRotation = Quaternion.Euler(arriba ? GiroPalanca : -GiroPalanca, 0f, 0f);
        }

        /// <summary>Hacia dónde apunta la palanca ahora mismo. Para los tests.</summary>
        public bool PalancaArriba { get { return _palancaArriba; } }

        public void RefrescarEnclavado(bool enclavado)
        {
            if (Accion != VdfAccionPulsador.Emergencia) return;

            _enclavado = enclavado;
            _restante = 0f;
            Hundir(enclavado);
        }

        /// <summary>El capuchón sobresale hacia -Z del tablero, así que hundirlo es ir a +Z.</summary>
        private void Hundir(bool hundido)
        {
            if (!_tengoReposo || Capuchon == null) return;
            Capuchon.localPosition = hundido ? _reposo + new Vector3(0f, 0f, Recorrido) : _reposo;
        }
    }
}
