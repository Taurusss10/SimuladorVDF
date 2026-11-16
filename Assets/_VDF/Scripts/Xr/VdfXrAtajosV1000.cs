using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace VDF
{
    /// <summary>
    /// Atajos del mando derecho para programar el V1000 sin tener que apuntar a cada tecla.
    ///
    /// Mientras el rayo del mando derecho está sobre el variador:
    /// - <b>A</b> hace de ENTER.
    /// - <b>B</b> hace de &gt;/RESET (mueve el dígito en edición).
    /// - <b>Joystick arriba/abajo</b> hace de ▲/▼; si se mantiene, repite solo.
    ///
    /// En ese tiempo el joystick derecho deja de teletransportar y de girar, para que el mismo
    /// gesto no haga dos cosas. Al dejar de apuntar al variador, todo vuelve a ser como antes.
    /// Las teclas pasan por <see cref="VfdKeypadView.Pulsar"/>, el mismo camino que el gatillo,
    /// así que el variador responde igual que si se pulsara la tecla.
    /// </summary>
    public class VdfXrAtajosV1000 : MonoBehaviour
    {
        public const string NombreObjeto = "VDF_AtajosV1000";

        /// <summary>Tiempo que se sigue considerando «apuntando» tras perder el rayo, para que
        /// el pulso de la mano no haga parpadear el modo.</summary>
        public const float Gracia = 0.35f;
        public const float UmbralJoystick = 0.6f;
        public const float EsperaRepeticion = 0.45f;
        public const float IntervaloRepeticion = 0.12f;

        private static readonly string[] AccionesLocomocionDerecha =
        {
            "XRI Right Locomotion/Teleport Mode",
            "XRI Right Locomotion/Snap Turn",
            "XRI Right Locomotion/Turn",
        };

        private VdfSimulacion _sim;
        private NearFarInteractor _derecho;
        private InputAction _a, _b, _joystick;
        private InputAction[] _locomocion;
        private float _ultimoApuntado = -10f;
        private bool _activo;
        private int _direccion;          // -1 abajo, 0 centro, +1 arriba
        private float _siguienteRepeticion;

        /// <summary>¿Está el mando derecho controlando el V1000 ahora mismo?</summary>
        public bool Activo => _activo;

        /// <summary>Crea el componente una sola vez en la escena de VR. Sin rig de VR no hace nada.</summary>
        public static VdfXrAtajosV1000 Asegurar()
        {
            if (Object.FindAnyObjectByType<NearFarInteractor>(FindObjectsInactive.Include) == null) return null;
            var existente = Object.FindAnyObjectByType<VdfXrAtajosV1000>();
            if (existente != null) return existente;
            return new GameObject(NombreObjeto).AddComponent<VdfXrAtajosV1000>();
        }

        private void OnEnable()
        {
            _a = new InputAction("V1000 Enter", InputActionType.Button);
            _a.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            _b = new InputAction("V1000 Reset", InputActionType.Button);
            _b.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            _joystick = new InputAction("V1000 Flechas", InputActionType.Value);
            _joystick.AddBinding("<XRController>{RightHand}/{Primary2DAxis}");
            _a.Enable(); _b.Enable(); _joystick.Enable();
        }

        private void OnDisable()
        {
            Desactivar();
            _a?.Dispose(); _b?.Dispose(); _joystick?.Dispose();
            _a = _b = _joystick = null;
        }

        private void Update()
        {
            if (_sim == null) _sim = Object.FindAnyObjectByType<VdfSimulacion>();
            if (_sim == null || _sim.Operador == null) return;
            if (_derecho == null) _derecho = BuscarInteractor(InteractorHandedness.Right);

            if (ApuntaAlVariador()) _ultimoApuntado = Time.unscaledTime;
            bool debeEstar = Time.unscaledTime - _ultimoApuntado <= Gracia;

            if (debeEstar && !_activo) Activar();
            else if (!debeEstar && _activo) Desactivar();

            // La pulsación se detecta por flanco del estado, comparando con el fotograma
            // anterior: es fiable venga la entrada del visor o de un mando simulado.
            bool a = _a.IsPressed(), b = _b.IsPressed();
            if (_activo)
            {
                if (a && !_aAntes) Pulsar(VfdKey.Enter);
                if (b && !_bAntes) Pulsar(VfdKey.Reset);
                LeerJoystick(_joystick.ReadValue<Vector2>().y);
            }
            _aAntes = a; _bAntes = b;
        }

        private bool _aAntes, _bAntes;

        /// <summary>
        /// Traduce el eje vertical del joystick a ▲/▼ con repetición: una pulsación al empujar
        /// y, si se mantiene, una cada <see cref="IntervaloRepeticion"/> tras una espera inicial.
        /// Público para poder probarlo sin mando.
        /// </summary>
        public void LeerJoystick(float y)
        {
            int dir = y > UmbralJoystick ? 1 : (y < -UmbralJoystick ? -1 : 0);
            if (dir == 0) { _direccion = 0; return; }

            float ahora = Time.unscaledTime;
            if (dir != _direccion)
            {
                _direccion = dir;
                _siguienteRepeticion = ahora + EsperaRepeticion;
                Pulsar(dir > 0 ? VfdKey.Arriba : VfdKey.Abajo);
            }
            else if (ahora >= _siguienteRepeticion)
            {
                _siguienteRepeticion = ahora + IntervaloRepeticion;
                Pulsar(dir > 0 ? VfdKey.Arriba : VfdKey.Abajo);
            }
        }

        private void Pulsar(VfdKey tecla)
        {
            _sim.Operador.Pulsar(tecla);
            VdfRespuestaPulsacion.Dar(_sim.Operador.transform.position, _derecho);
        }

        /// <summary>¿El rayo derecho está sobre alguna pieza del variador?</summary>
        private bool ApuntaAlVariador()
        {
            if (_derecho == null) return false;
            Transform variador = RaizDelVariador();
            foreach (var h in _derecho.interactablesHovered)
                if (h != null && h.transform != null && h.transform.IsChildOf(variador)) return true;
            return false;
        }

        private Transform RaizDelVariador()
        {
            var op = _sim.Operador.transform;
            return op.parent != null ? op.parent : op;
        }

        private void Activar()
        {
            _activo = true;
            _direccion = 0;
            // Se apagan solo las que estaban encendidas, y solo esas se vuelven a encender:
            // el giro continuo está apagado a propósito y no debe revivir al salir.
            _apagadas.Clear();
            foreach (var accion in Locomocion())
                if (accion.enabled) { accion.Disable(); _apagadas.Add(accion); }
        }

        private void Desactivar()
        {
            if (!_activo) return;
            _activo = false;
            foreach (var accion in _apagadas) accion.Enable();
            _apagadas.Clear();
        }

        private readonly System.Collections.Generic.List<InputAction> _apagadas =
            new System.Collections.Generic.List<InputAction>();

        /// <summary>¿Puede el joystick derecho teletransportar ahora? Para las pruebas.
        /// Devuelve null si el rig no tiene esa acción.</summary>
        public bool? LocomocionDerechaHabilitada()
        {
            foreach (var accion in Locomocion())
                if (accion.name == "Teleport Mode") return accion.enabled;
            return null;
        }

        private InputAction[] Locomocion()
        {
            if (_locomocion != null) return _locomocion;
            var lista = new System.Collections.Generic.List<InputAction>();
            var gestor = Object.FindAnyObjectByType<InputActionManager>();
            if (gestor != null && gestor.actionAssets != null)
                foreach (var asset in gestor.actionAssets)
                {
                    if (asset == null) continue;
                    foreach (var nombre in AccionesLocomocionDerecha)
                    {
                        var accion = asset.FindAction(nombre);
                        if (accion != null) lista.Add(accion);
                    }
                }
            _locomocion = lista.ToArray();
            return _locomocion;
        }

        public static NearFarInteractor BuscarInteractor(InteractorHandedness mano)
        {
            foreach (var nf in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Exclude))
                if (nf.handedness == mano) return nf;
            return null;
        }
    }
}
