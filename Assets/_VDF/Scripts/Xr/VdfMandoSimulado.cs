using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VDF
{
    public static class VdfMandoSimulado
    {
        public const string NombreDerecho = "VdfSimDer";
        public const string NombreIzquierdo = "VdfSimIzq";

        /// <summary>
        /// Quita el simulador de XRI (que en el editor se crea solo, nunca en el APK) y sus
        /// mandos, para que no pisen a los nuestros. Hay que dejar pasar un frame antes de
        /// <see cref="Conectar"/>: si se quitan y se ponen en el mismo frame, el gestor de
        /// modalidad del rig ve la desconexión al final y apaga los mandos.
        /// </summary>
        public static void Preparar()
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                if (mb != null && mb.GetType().Name.Contains("Simulator")) mb.enabled = false;

            QuitarDispositivos();
        }

        public static void Conectar()
        {
            var der = InputSystem.AddDevice<XRSimulatedController>(NombreDerecho);
            InputSystem.SetDeviceUsage(der, UnityEngine.InputSystem.CommonUsages.RightHand);
            var izq = InputSystem.AddDevice<XRSimulatedController>(NombreIzquierdo);
            InputSystem.SetDeviceUsage(izq, UnityEngine.InputSystem.CommonUsages.LeftHand);

            // El izquierdo, apuntando al suelo y apartado, para que no estorbe.
            Poner(false, new Vector3(-0.35f, 0.9f, -0.85f), Quaternion.LookRotation(Vector3.down), false, false);
        }

        public static void Limpiar()
        {
            QuitarDispositivos();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
        }

        /// <summary>Apunta el mando derecho a <paramref name="objetivo"/> desde unos 30 cm delante.</summary>
        public static void Apuntar(Vector3 objetivo, bool gatillo = false, bool lateral = false)
        {
            Apuntar(objetivo, gatillo, lateral, false, false, Vector2.zero);
        }

        /// <summary>
        /// Igual que <see cref="Apuntar(Vector3, bool, bool)"/>, con los botones A y B y el
        /// joystick del mando derecho.
        /// </summary>
        public static void Apuntar(Vector3 objetivo, bool gatillo, bool lateral, bool a, bool b, Vector2 joystick)
        {
            Vector3 desde = objetivo + new Vector3(0.04f, 0.08f, -0.30f);
            Poner(true, desde, Quaternion.LookRotation(objetivo - desde, Vector3.up), gatillo, lateral, a, b, joystick);
        }

        /// <summary>El mando izquierdo delante del estudiante, con X e Y como se pidan.</summary>
        public static void ManoIzquierda(Vector3 posicion, bool x, bool y)
        {
            Poner(false, posicion, Quaternion.LookRotation(Vector3.forward, Vector3.up), false, false, x, y, Vector2.zero);
        }

        /// <summary>
        /// Lleva la PUNTA del mando derecho (la del toque) a <paramref name="punto"/>, con el
        /// mando mirando al tablero. <paramref name="retroceso"/> la deja esa distancia antes.
        /// </summary>
        public static void Tocar(Vector3 punto, float retroceso)
        {
            var rot = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Vector3 desfase = Vector3.zero;
            foreach (var poke in Object.FindObjectsByType<XRPokeInteractor>())
            {
                if (!EsDerecho(poke.transform)) continue;
                var mando = BuscarMando(poke.transform);
                var punta = poke.attachTransform != null ? poke.attachTransform : poke.transform;
                desfase = Quaternion.Inverse(mando.rotation) * (punta.position - mando.position);
                break;
            }
            Vector3 destinoPunta = punto + Vector3.back * retroceso;
            Poner(true, destinoPunta - rot * desfase, rot, false, false);
        }

        /// <summary>Estado del mando derecho visto por el interactor. Para los mensajes de los tests.</summary>
        public static string Estado()
        {
            foreach (var nf in Object.FindObjectsByType<NearFarInteractor>())
            {
                if (!EsDerecho(nf.transform)) continue;
                return "activar=" + nf.activateInput.ReadIsPerformed() + " seleccionar=" + nf.selectInput.ReadIsPerformed()
                       + " activarApuntado=" + nf.allowHoveredActivate + " seleccionando=" + nf.hasSelection;
            }
            return "sin mando derecho";
        }

        /// <summary>
        /// Lleva el punto de agarre del mando derecho (el de la esfera cercana) a
        /// <paramref name="punto"/>, apuntando al tablero. Con <paramref name="lateral"/> el
        /// gatillo lateral queda apretado: así se coge y se sostiene un cable.
        /// </summary>
        public static void LlevarMano(Vector3 punto, bool lateral)
        {
            var rot = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            Vector3 desfase = Vector3.zero;
            foreach (var nf in Object.FindObjectsByType<NearFarInteractor>())
            {
                if (!EsDerecho(nf.transform)) continue;
                var mando = BuscarMando(nf.transform);
                var agarre = nf.attachTransform != null ? nf.attachTransform : nf.transform;
                desfase = Quaternion.Inverse(mando.rotation) * (agarre.position - mando.position);
                break;
            }
            Poner(true, punto - rot * desfase, rot, false, lateral);
        }

        /// <summary>Lo que sostiene ahora el mando derecho, o null.</summary>
        public static string Sostenido()
        {
            foreach (var nf in Object.FindObjectsByType<NearFarInteractor>())
            {
                if (!EsDerecho(nf.transform)) continue;
                foreach (var s in nf.interactablesSelected) return s.transform.name;
            }
            return null;
        }

        /// <summary>Lo que tiene el rayo del mando derecho encima, o null.</summary>
        public static string Apuntado()
        {
            foreach (var nf in Object.FindObjectsByType<NearFarInteractor>())
            {
                if (!EsDerecho(nf.transform)) continue;
                foreach (var h in nf.interactablesHovered) return h.transform.name;
            }
            return null;
        }

        // =====================================================================================

        private static void Poner(bool derecho, Vector3 posicion, Quaternion rotacion, bool gatillo, bool lateral)
        {
            Poner(derecho, posicion, rotacion, gatillo, lateral, false, false, Vector2.zero);
        }

        private static void Poner(bool derecho, Vector3 posicion, Quaternion rotacion, bool gatillo, bool lateral,
                                  bool primario, bool secundario, Vector2 joystick)
        {
            var dispositivo = Buscar(derecho ? NombreDerecho : NombreIzquierdo);
            var origen = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (dispositivo == null || origen == null) return;

            var offset = origen.CameraFloorOffsetObject.transform;
            var st = new XRSimulatedControllerState
            {
                devicePosition = offset.InverseTransformPoint(posicion),
                deviceRotation = Quaternion.Inverse(offset.rotation) * rotacion,
                isTracked = true,
                trackingState = 3,
                trigger = gatillo ? 1f : 0f,
                grip = lateral ? 1f : 0f,
                primary2DAxis = joystick,
            };
            if (gatillo) st = st.WithButton(ControllerButton.TriggerButton, true);
            if (lateral) st = st.WithButton(ControllerButton.GripButton, true);
            if (primario) st = st.WithButton(ControllerButton.PrimaryButton, true);
            if (secundario) st = st.WithButton(ControllerButton.SecondaryButton, true);
            InputSystem.QueueStateEvent(dispositivo, st);
        }

        private static XRSimulatedController Buscar(string nombre)
        {
            foreach (var d in InputSystem.devices)
                if (d.name == nombre && d is XRSimulatedController) return (XRSimulatedController)d;
            return null;
        }

        private static void QuitarDispositivos()
        {
            var quitar = new List<InputDevice>();
            foreach (var d in InputSystem.devices) if (d is XRSimulatedController) quitar.Add(d);
            foreach (var d in quitar) InputSystem.RemoveDevice(d);
        }

        private static bool EsDerecho(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (p.name.Contains("Right")) return true;
            return false;
        }

        private static Transform BuscarMando(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (p.name == "Right Controller" || p.name == "Left Controller") return p;
            return t;
        }
    }
}
