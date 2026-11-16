// NOTA: un MonoBehaviour por archivo. Ver E6 en VIABILIDAD.md.

using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Guía paso a paso en pantalla para la práctica de operación (práctica 4).
    ///
    /// Los pasos **no se marcan a mano**: cada uno tiene una condición que se evalúa contra
    /// el estado real del simulador. Si el alumno hace las cosas en otro orden, o retrocede,
    /// la guía lo refleja. Eso evita el típico tutorial que dice "hecho" cuando no lo está.
    ///
    /// ⚠️ Usa OnGUI, que es suficiente para el modo escritorio pero **no sirve en VR**.
    /// Para el visor hay que rehacerla como UI en espacio de mundo. Ver E8 en el informe.
    /// </summary>
    public class VdfGuiaPractica : MonoBehaviour
    {
        public VdfSimulacion Simulacion;

        [Tooltip("Mostrar u ocultar la guía con la tecla H.")]
        public bool Visible = true;

        /// <summary>Un paso de la guía y la condición que lo da por cumplido.</summary>
        private struct Paso
        {
            public string Texto;
            public System.Func<VdfSimulacion, bool> Cumplido;

            public Paso(string texto, System.Func<VdfSimulacion, bool> cumplido)
            {
                Texto = texto;
                Cumplido = cumplido;
            }
        }

        private Paso[] _pasos;
        private GUIStyle _estiloTitulo, _estiloPaso, _estiloPasoHecho, _estiloPasoActual, _estiloEstado;

        // --- Hitos ya conseguidos -----------------------------------------------------------
        //
        // Los pasos de una secuencia NO se pueden evaluar contra el estado actual: al bajar
        // de velocidad 4 a 1, "subir hasta velocidad 4" se des-marcaba, y al pulsar STOP se
        // des-marcaba "subir a velocidad 1". El alumno veía su progreso deshacerse mientras
        // hacía justo lo que se le pedía.
        //
        // Contra el estado actual se evalúa solo lo que de verdad es una CONDICIÓN
        // (configuración, cableado, tensión). Lo que es un LOGRO se recuerda.
        private bool _arranco;
        private int _nivelMaximoAlcanzado;
        private bool _bajoHastaUno;
        private bool _paroDespuesDeOperar;

        private void Awake()
        {
            if (Simulacion == null) Simulacion = Object.FindAnyObjectByType<VdfSimulacion>();
            ConstruirPasos();
        }

        private void ConstruirPasos()
        {
            _pasos = new[]
            {
                new Paso("1. Configurar el variador (A1-03=2220 y la tabla de parametros)",
                         s => s.Vfd.ConfiguracionEsOperable() && s.Vfd.ValidarConfiguracion().Count == 0),

                new Paso("2. Cablear el tablero (19 cables, 14 redes)",
                         s => s.InformeCableado != null && s.InformeCableado.EsCorrecto),

                new Paso("3. Comprobar que el variador tiene tension",
                         s => s.Vfd.Alimentado),

                new Paso("4. Pulsar START para habilitar el sistema   [Q]",
                         s => _arranco),

                new Paso("5. Subir a velocidad 1 (10 Hz)   [flecha arriba]",
                         s => _nivelMaximoAlcanzado >= 1),

                new Paso("6. Subir hasta velocidad 4 (50 Hz), una pulsacion por nivel",
                         s => _nivelMaximoAlcanzado >= 4),

                new Paso("7. Bajar hasta velocidad 1   [flecha abajo]",
                         s => _bajoHastaUno),

                new Paso("8. Pulsar STOP para parar y reiniciar la seleccion   [W]",
                         s => _paroDespuesDeOperar)
            };
        }

        private void Update()
        {
            if (Simulacion == null) return;

            var teclado = UnityEngine.InputSystem.Keyboard.current;
            if (teclado != null && teclado.hKey.wasPressedThisFrame) Visible = !Visible;

            var plc = Simulacion.Plc;
            if (plc == null) return;

            if (plc.Habilitado) _arranco = true;

            if (plc.NivelActivo > _nivelMaximoAlcanzado)
                _nivelMaximoAlcanzado = plc.NivelActivo;

            // Bajar solo cuenta si antes se llego arriba del todo.
            if (_nivelMaximoAlcanzado >= 4 && plc.Habilitado && plc.NivelActivo == 1)
                _bajoHastaUno = true;

            // Y parar solo cuenta si antes se estuvo operando.
            if (_bajoHastaUno && !plc.Habilitado && plc.NivelActivo == 0)
                _paroDespuesDeOperar = true;
        }

        /// <summary>Índice del primer paso sin cumplir, o -1 si están todos.</summary>
        public int PasoActual()
        {
            if (Simulacion == null) return 0;

            for (int i = 0; i < _pasos.Length; i++)
                if (!_pasos[i].Cumplido(Simulacion)) return i;
            return -1;
        }

        public bool PasoCumplido(int i)
        {
            if (Simulacion == null || i < 0 || i >= _pasos.Length) return false;
            return _pasos[i].Cumplido(Simulacion);
        }

        public int TotalPasos { get { return _pasos != null ? _pasos.Length : 0; } }

        /// <summary>Reinicia los hitos, para volver a empezar la practica.</summary>
        public void Reiniciar()
        {
            _arranco = false;
            _nivelMaximoAlcanzado = 0;
            _bajoHastaUno = false;
            _paroDespuesDeOperar = false;
        }

        public string TextoPaso(int i)
        {
            return (_pasos != null && i >= 0 && i < _pasos.Length) ? _pasos[i].Texto : "";
        }

        private void PrepararEstilos()
        {
            if (_estiloTitulo != null) return;

            _estiloTitulo = new GUIStyle(GUI.skin.label)
            { fontSize = 17, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };

            _estiloPaso = new GUIStyle(GUI.skin.label)
            { fontSize = 13, normal = { textColor = new Color(0.72f, 0.74f, 0.78f) } };

            _estiloPasoHecho = new GUIStyle(_estiloPaso)
            { normal = { textColor = new Color(0.35f, 0.88f, 0.45f) } };

            _estiloPasoActual = new GUIStyle(_estiloPaso)
            { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.25f) } };

            _estiloEstado = new GUIStyle(GUI.skin.label)
            { fontSize = 13, normal = { textColor = new Color(0.85f, 0.90f, 1f) } };
        }

        private void OnGUI()
        {
            if (!Visible || Simulacion == null || _pasos == null) return;

            PrepararEstilos();

            const float ancho = 620f;
            float alto = 150f + _pasos.Length * 20f;

            GUI.Box(new Rect(12, 12, ancho, alto), GUIContent.none);
            GUILayout.BeginArea(new Rect(26, 24, ancho - 28, alto - 20));

            GUILayout.Label("PRACTICA 4 — Operacion del modulo VDF", _estiloTitulo);
            GUILayout.Space(6);

            int actual = PasoActual();
            for (int i = 0; i < _pasos.Length; i++)
            {
                bool hecho = PasoCumplido(i);
                string marca = hecho ? "[x] " : (i == actual ? " >  " : "[ ] ");
                var estilo = hecho ? _estiloPasoHecho : (i == actual ? _estiloPasoActual : _estiloPaso);
                GUILayout.Label(marca + _pasos[i].Texto, estilo);
            }

            GUILayout.Space(8);
            GUILayout.Label(Simulacion.Resumen(), _estiloEstado);

            if (Simulacion.InformeCableado != null && !Simulacion.InformeCableado.EsCorrecto)
            {
                int faltan = Simulacion.InformeCableado.Faltantes.Count;
                int sobran = Simulacion.InformeCableado.Sobrantes.Count;
                GUILayout.Label("Cableado: faltan " + faltan + " uniones, sobran " + sobran, _estiloEstado);
                if (faltan > 0) GUILayout.Label("   -> " + Simulacion.InformeCableado.Faltantes[0], _estiloPaso);
            }

            GUILayout.Space(6);
            bool interruptores = Simulacion != null && Simulacion.ModoMando == VdfModoMando.Interruptores;
            GUILayout.Label(interruptores
                ? "A interruptor S4   S interruptor S5   D interruptor S6   ESPACIO emergencia   R rearmar   H ocultar"
                : "Q start   W stop   flechas subir/bajar   ESPACIO emergencia   R rearmar   H ocultar",
                _estiloPaso);
            GUILayout.Label("Camara: boton CENTRAL arrastrar para girar   RUEDA acercar   " +
                "F1 conjunto  F2 teclado  F3 regleta  F4 Ladder", _estiloPaso);

            // Los cables tambien, y no es de adorno: la practica 3 es cablear de principio a
            // fin, y como se hace solo se decia en la pista de UN paso. En cuanto el alumno
            // pasaba ese paso, se quedaba sin saber como quitar un cable mal puesto salvo que
            // hubiera leido el LEEME, que es justo lo que nadie hace.
            GUILayout.Label("Cables: CLIC en un borne y CLIC en el otro   " +
                "CLIC DERECHO quitar   1-6 color   ESC cancelar", _estiloPaso);

            if (actual < 0) GUILayout.Label("PRACTICA COMPLETADA", _estiloPasoHecho);

            GUILayout.EndArea();
        }
    }
}
