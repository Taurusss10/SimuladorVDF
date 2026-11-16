using System.Collections.Generic;
using UnityEngine;

namespace VDF
{
    /// <summary>
    /// Modelo del variador Yaskawa V1000 en C# puro: sin MonoBehaviour, sin escena,
    /// sin Time.deltaTime implícito. Avanza con <see cref="Tick"/>, lo que permite
    /// probarlo en EditMode con tiempo simulado y reutilizarlo igual en VR y en escritorio.
    ///
    /// Alcance deliberadamente limitado (según el enunciado): NO modela el circuito
    /// eléctrico interno, par, deslizamiento ni curva V/f real. Modela la frecuencia de
    /// salida, las rampas, la selección de velocidad por terminales y los errores de
    /// configuración que la práctica debe enseñar a detectar.
    /// </summary>
    public sealed class VfdModel
    {
        /// <summary>Terminales de entrada digital del V1000: S1 .. S6.</summary>
        public const int PrimerTerminal = 1;
        public const int UltimoTerminal = 6;

        private const float Epsilon = 0.001f;

        private readonly Dictionary<string, float> _valores = new Dictionary<string, float>();
        private readonly bool[] _terminales = new bool[UltimoTerminal + 1];

        /// <summary>Tensión presente en R/L1 - S/L2 (la decide el cableado, ver E4).</summary>
        public bool Alimentado { get; private set; }

        /// <summary>Referencia que entraría por la entrada analógica A1 cuando b1-01 = 1.</summary>
        public float ReferenciaAnalogicaHz { get; set; }

        /// <summary>
        /// Modo LOCAL (tecla LO/RE del teclado). En LOCAL la orden de marcha viene de la
        /// tecla RUN y no de los terminales, pase lo que pase con b1-02.
        /// </summary>
        public bool ModoLocal { get; set; }

        /// <summary>Tecla RUN del operador digital pulsada (solo tiene efecto en LOCAL o con b1-02 = 0).</summary>
        public bool MarchaDesdeOperador { get; set; }

        public bool MarchaBloqueada { get; set; }

        public VfdState Estado { get; private set; }
        public VfdFault Falla { get; private set; }

        /// <summary>Frecuencia realmente entregada al motor, tras las rampas.</summary>
        public float FrecuenciaSalidaHz { get; private set; }

        /// <summary>Frecuencia objetivo seleccionada por los terminales, antes de las rampas.</summary>
        public float ReferenciaFrecuenciaHz { get; private set; }

        /// <summary>Sentido de giro inverso activo (terminal con función 41).</summary>
        public bool Reversa { get; private set; }

        // --- Indicadores del teclado ------------------------------------------------------
        public bool LedAlm { get { return Falla != VfdFault.Ninguna; } }
        public bool LedRev { get { return Reversa; } }
        public bool LedDrv { get { return Alimentado; } }

        public VfdModel()
        {
            RestaurarValoresDeFabrica();
            Alimentado = true;
        }

        // =====================================================================================
        // Parámetros
        // =====================================================================================

        public void RestaurarValoresDeFabrica()
        {
            _valores.Clear();
            var todos = VfdParameterTable.Todos;
            for (int i = 0; i < todos.Count; i++)
                _valores[todos[i].Id] = todos[i].PorDefecto;

            FrecuenciaSalidaHz = 0f;
            ReferenciaFrecuenciaHz = 0f;
            Estado = VfdState.Detenido;
            Falla = VfdFault.Ninguna;
            Reversa = false;
            ModoLocal = false;
            MarchaDesdeOperador = false;
            for (int t = 0; t < _terminales.Length; t++) _terminales[t] = false;
        }

        public float Valor(string id)
        {
            float v;
            if (_valores.TryGetValue(id, out v)) return v;
            throw new KeyNotFoundException("Parametro desconocido: " + id);
        }

        public int ValorEntero(string id)
        {
            return Mathf.RoundToInt(Valor(id));
        }

        /// <summary>
        /// Escribe un parámetro validando rango y resolución, igual que haría el teclado.
        /// Escribir A1-03 = 2220 ejecuta la inicialización a 2 hilos (comportamiento real:
        /// restaura valores de fábrica y A1-03 vuelve a 0).
        /// </summary>
        public bool EscribirParametro(string id, float valor, out string error)
        {
            VfdParameterDef def;
            if (!VfdParameterTable.TryGet(id, out def))
            {
                error = "Parametro inexistente: " + id;
                return false;
            }

            float cuantizado = def.Cuantizar(valor);
            if (!def.EsValido(cuantizado, out error))
                return false;

            if (id == "A1-03" && cuantizado > 0f)
            {
                Inicializar(Mathf.RoundToInt(cuantizado));
                error = null;
                return true;
            }

            _valores[id] = cuantizado;
            error = null;
            return true;
        }

        public bool EscribirParametro(string id, float valor)
        {
            string _;
            return EscribirParametro(id, valor, out _);
        }

        /// <summary>Cuántas veces se ha inicializado por A1-03. Lo mira la práctica 2.</summary>
        public int Inicializaciones { get; private set; }

        /// <summary>
        /// Inicialización por A1-03. 2220 = secuencia de 2 hilos, 3330 = 3 hilos,
        /// 1110 = valores de usuario. Tras ejecutarse, A1-03 vuelve a 0.
        /// </summary>
        public void Inicializar(int modo)
        {
            Inicializaciones++;
            RestaurarValoresDeFabrica();

            if (modo == 2220)
            {
                _valores["H1-01"] = VfdTerminalFunction.MarchaAdelante2Hilos;
                _valores["H1-02"] = VfdTerminalFunction.MarchaAtras2Hilos;
            }
            else if (modo == 3330)
            {
                _valores["H1-01"] = VfdTerminalFunction.MarchaAdelante2Hilos;
                _valores["H1-02"] = VfdTerminalFunction.MarchaAtras2Hilos;
                _valores["H1-03"] = VfdTerminalFunction.TresHilos;
            }

            _valores["A1-03"] = 0f;
        }

        /// <summary>Aplica de una vez la configuración correcta de la práctica. Útil en tests y en el modo "saltar configuración".</summary>
        public void AplicarConfiguracionDeReferencia()
        {
            foreach (var kv in VfdParameterTable.ReferenciaActiva)
                _valores[kv.Key] = kv.Value;
        }

        // =====================================================================================
        // Terminales de entrada
        // =====================================================================================

        public void SetTerminal(int numero, bool activo)
        {
            if (numero < PrimerTerminal || numero > UltimoTerminal)
                throw new System.ArgumentOutOfRangeException("numero", "Terminal valido: S1..S6");
            _terminales[numero] = activo;
        }

        public bool GetTerminal(int numero)
        {
            if (numero < PrimerTerminal || numero > UltimoTerminal) return false;
            return _terminales[numero];
        }

        /// <summary>Código de función (H1-0x) asignado al terminal S{numero}.</summary>
        public int FuncionDeTerminal(int numero)
        {
            return ValorEntero("H1-0" + numero);
        }

        /// <summary>¿Hay algún terminal con esta función y además activo?</summary>
        private bool HayTerminalActivoConFuncion(int funcion)
        {
            for (int t = PrimerTerminal; t <= UltimoTerminal; t++)
                if (_terminales[t] && FuncionDeTerminal(t) == funcion)
                    return true;
            return false;
        }

        /// <summary>¿Hay algún terminal al que se le haya asignado esta función (activo o no)?</summary>
        public bool ExisteTerminalConFuncion(int funcion)
        {
            for (int t = PrimerTerminal; t <= UltimoTerminal; t++)
                if (FuncionDeTerminal(t) == funcion)
                    return true;
            return false;
        }

        // =====================================================================================
        // Alimentación y fallas
        // =====================================================================================

        public void SetAlimentacion(bool energizado)
        {
            Alimentado = energizado;
            if (!energizado)
            {
                FrecuenciaSalidaHz = 0f;
                ReferenciaFrecuenciaHz = 0f;
                Estado = VfdState.Detenido;
            }
        }

        /// <summary>Corte inmediato por parada de emergencia: el motor queda libre (coast to stop).</summary>
        public void ParadaEmergencia()
        {
            Falla = VfdFault.ParadaEmergencia;
            Estado = VfdState.Falla;
            FrecuenciaSalidaHz = 0f;
            ReferenciaFrecuenciaHz = 0f;
        }

        public void DispararFalla(VfdFault falla)
        {
            if (falla == VfdFault.Ninguna) return;
            Falla = falla;
            Estado = VfdState.Falla;
            FrecuenciaSalidaHz = 0f;
            ReferenciaFrecuenciaHz = 0f;
        }

        /// <summary>Tecla &gt;/RESET: borra la falla si la causa ya no está presente.</summary>
        public bool ResetFalla()
        {
            if (Falla == VfdFault.Ninguna) return true;
            if (Falla == VfdFault.BajoVoltaje && !Alimentado) return false;

            Falla = VfdFault.Ninguna;
            Estado = VfdState.Detenido;
            return true;
        }

        // =====================================================================================
        // Selección de velocidad
        // =====================================================================================

        /// <summary>
        /// Índice de velocidad multi-step seleccionado por los terminales: 0..3,
        /// que corresponde a d1-01 .. d1-04.
        ///
        /// Tabla del manual oficial: ninguna entrada -> d1-01; solo multi-vel.1 -> d1-02;
        /// solo multi-vel.2 -> d1-03; ambas -> d1-04.
        ///
        /// Se resuelve por FUNCIÓN asignada, no por número de terminal. Por eso, si el
        /// alumno invierte H1-05/H1-06 (el error del manual del módulo), las velocidades
        /// 2 y 3 se intercambian de verdad, igual que en el equipo real.
        /// </summary>
        public int IndiceVelocidadSeleccionada()
        {
            bool bit1 = HayTerminalActivoConFuncion(VfdTerminalFunction.MultiVelocidad1);
            bool bit2 = HayTerminalActivoConFuncion(VfdTerminalFunction.MultiVelocidad2);
            return (bit1 ? 1 : 0) | (bit2 ? 2 : 0);
        }

        /// <summary>Frecuencia objetivo según la selección actual, ya limitada por E1-04.</summary>
        public float ResolverReferenciaHz()
        {
            int indice = IndiceVelocidadSeleccionada();
            float hz;

            if (indice == 0)
            {
                // Sin ninguna entrada multi-velocidad activa, la referencia viene de b1-01.
                // b1-01 = 0 -> operador (d1-01). b1-01 = 1 -> entrada analogica A1.
                hz = ValorEntero("b1-01") == 0 ? Valor("d1-01") : ReferenciaAnalogicaHz;
            }
            else
            {
                hz = Valor("d1-0" + (indice + 1));
            }

            return Mathf.Clamp(hz, 0f, Valor("E1-04"));
        }

        /// <summary>¿Los terminales están pidiendo marcha ahora mismo?</summary>
        public bool OrdenDeMarchaActiva()
        {
            return !MarchaBloqueada && PideMarcha();
        }

        /// <summary>¿Alguien está pidiendo marcha (teclado o terminales), aunque esté bloqueada?</summary>
        public bool PideMarcha()
        {
            // En LOCAL manda el teclado. Tambien si b1-02 = 0 (orden desde el operador).
            if (ModoLocal || ValorEntero("b1-02") == 0) return MarchaDesdeOperador;

            // b1-02 = 1 -> la orden de marcha viene de los terminales.
            if (ValorEntero("b1-02") != 1) return false;

            bool adelante = HayTerminalActivoConFuncion(VfdTerminalFunction.MarchaAdelante2Hilos);
            bool atras = HayTerminalActivoConFuncion(VfdTerminalFunction.MarchaAtras2Hilos);

            // Adelante y atras a la vez: el V1000 lo trata como orden de parada.
            if (adelante && atras) return false;

            return adelante || atras;
        }

        // =====================================================================================
        // Avance del tiempo
        // =====================================================================================

        /// <summary>
        /// Avanza el modelo dt segundos. Las rampas C1-01/C1-02 son tiempos de 0 a E1-04
        /// (frecuencia máxima), como define el manual, no de 0 a la referencia actual.
        /// </summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            if (!Alimentado)
            {
                FrecuenciaSalidaHz = 0f;
                ReferenciaFrecuenciaHz = 0f;
                if (Estado != VfdState.Falla) Estado = VfdState.Detenido;
                return;
            }

            if (Estado == VfdState.Falla)
            {
                FrecuenciaSalidaHz = 0f;
                ReferenciaFrecuenciaHz = 0f;
                return;
            }

            bool marcha = OrdenDeMarchaActiva();
            Reversa = marcha && HayTerminalActivoConFuncion(VfdTerminalFunction.MarchaAtras2Hilos);

            float objetivo = marcha ? ResolverReferenciaHz() : 0f;
            ReferenciaFrecuenciaHz = objetivo;

            float fMax = Mathf.Max(Epsilon, Valor("E1-04"));
            float tAcc = Mathf.Max(0.01f, Valor("C1-01"));
            float tDec = Mathf.Max(0.01f, Valor("C1-02"));
            float pasoSubida = (fMax / tAcc) * dt;
            float pasoBajada = (fMax / tDec) * dt;

            if (objetivo > FrecuenciaSalidaHz)
                FrecuenciaSalidaHz = Mathf.Min(objetivo, FrecuenciaSalidaHz + pasoSubida);
            else if (objetivo < FrecuenciaSalidaHz)
                FrecuenciaSalidaHz = Mathf.Max(objetivo, FrecuenciaSalidaHz - pasoBajada);

            if (Mathf.Abs(FrecuenciaSalidaHz - objetivo) <= Epsilon)
            {
                FrecuenciaSalidaHz = objetivo;
                Estado = objetivo > Epsilon ? VfdState.EnMarcha : VfdState.Detenido;
            }
            else
            {
                Estado = FrecuenciaSalidaHz < objetivo ? VfdState.Acelerando : VfdState.Desacelerando;
            }
        }

        // =====================================================================================
        // Validación didáctica de la configuración
        // =====================================================================================

        /// <summary>
        /// Compara la configuración cargada con la de referencia y explica qué efecto real
        /// tiene cada desviación. Es el núcleo de la práctica 5 (errores de configuración).
        /// </summary>
        public List<VfdConfigIssue> ValidarConfiguracion()
        {
            var problemas = new List<VfdConfigIssue>();
            bool laboratorio = VdfMando.Laboratorio;
            var referencia = VfdParameterTable.ReferenciaActiva;

            if (ModoLocal)
            {
                problemas.Add(new VfdConfigIssue("LO/RE", VfdIssueSeverity.Critica,
                    "El variador esta en LOCAL: las ordenes vienen del teclado y los " +
                    "terminales no mandan, asi que el PLC no puede arrancar el motor. " +
                    "Pulsa LO/RE en el operador para volver a REMOTO."));
            }

            // --- 1. ¿Puede siquiera arrancar? ------------------------------------------------
            if (ValorEntero("b1-02") != 1)
            {
                problemas.Add(new VfdConfigIssue("b1-02", VfdIssueSeverity.Critica,
                    "La orden de marcha no viene de los terminales (b1-02 = " + ValorEntero("b1-02") +
                    "). Las salidas del PLC no arrancan el motor. Debe ser 1."));
            }
            else if (laboratorio && ValorEntero("H1-04") != VfdTerminalFunction.MarchaAdelante2Hilos)
            {
                // Práctica del laboratorio: Q0.5 llega a S4, así que la marcha tiene que estar
                // en S4. De fábrica S4 es el reset de falla (14), y con eso el PLC «resetea»
                // en vez de arrancar.
                int h104 = ValorEntero("H1-04");
                string detalle = h104 == 14
                    ? "H1-04 = 14 deja S4 como reset de falla, que es su valor de fabrica. La marcha " +
                      "del PLC llega por S4 (Q0.5), asi que el motor no arranca. Debe ser 40."
                    : "H1-04 = " + h104 + ": S4 recibe la marcha del PLC (Q0.5) pero no tiene la " +
                      "funcion de marcha adelante. Debe ser 40.";
                problemas.Add(new VfdConfigIssue("H1-04", VfdIssueSeverity.Critica, detalle));
            }
            else if (!ExisteTerminalConFuncion(VfdTerminalFunction.MarchaAdelante2Hilos))
            {
                int h101 = ValorEntero("H1-01");
                string detalle = h101 == VfdTerminalFunction.LocalRemote
                    ? "H1-01 = 1 asigna a S1 la funcion LOCAL/REMOTE, que NO es una orden de marcha. " +
                      "Para este modelo el valor correcto es 40."
                    : "Ningun terminal tiene la funcion 40 (marcha adelante, 2 hilos), asi que nada puede arrancar el motor.";

                problemas.Add(new VfdConfigIssue("H1-01", VfdIssueSeverity.Critica, detalle));
            }

            // En el laboratorio S1 se queda sin cable y se programa como reset de falla. Con 40
            // el motor funciona igual (S1 no recibe nada), pero la configuración no es la de
            // la práctica, y dos terminales con la misma marcha es una trampa para el siguiente.
            if (laboratorio && ValorEntero("H1-01") != 14 &&
                ValorEntero("H1-04") == VfdTerminalFunction.MarchaAdelante2Hilos)
            {
                problemas.Add(new VfdConfigIssue("H1-01", VfdIssueSeverity.Advertencia,
                    "H1-01 = " + ValorEntero("H1-01") + ": en esta practica la marcha va por S4 y " +
                    "S1 queda como reset de falla. Debe ser 14."));
            }

            // --- 2. Selección de velocidad ---------------------------------------------------
            bool hayMulti1 = ExisteTerminalConFuncion(VfdTerminalFunction.MultiVelocidad1);
            bool hayMulti2 = ExisteTerminalConFuncion(VfdTerminalFunction.MultiVelocidad2);

            if (!hayMulti1 || !hayMulti2)
            {
                problemas.Add(new VfdConfigIssue(!hayMulti1 ? "H1-05" : "H1-06", VfdIssueSeverity.Critica,
                    "Falta asignar la funcion multi-velocidad " + (!hayMulti1 ? "1 (valor 3)" : "2 (valor 4)") +
                    ". Sin las dos, no se pueden alcanzar las 4 velocidades."));
            }
            else if (ValorEntero("H1-05") == VfdTerminalFunction.MultiVelocidad2 &&
                     ValorEntero("H1-06") == VfdTerminalFunction.MultiVelocidad1)
            {
                problemas.Add(new VfdConfigIssue("H1-05", VfdIssueSeverity.Advertencia,
                    "H1-05 y H1-06 estan invertidos: deben ser 3 y 4. El motor gira, pero las " +
                    "velocidades 2 y 3 salen intercambiadas (S5 solo da d1-03 en vez de d1-02)."));
            }

            // --- 3. Referencia de frecuencia -------------------------------------------------
            if (ValorEntero("b1-01") != 0)
            {
                problemas.Add(new VfdConfigIssue("b1-01", VfdIssueSeverity.Advertencia,
                    "b1-01 = " + ValorEntero("b1-01") + ": la velocidad 1 no se toma de d1-01 sino de la " +
                    "entrada analogica A1. Con A1 sin cablear, la velocidad 1 queda en 0 Hz mientras que " +
                    "las velocidades 2 a 4 si funcionan. Debe ser 0."));
            }

            // --- 4. Velocidades inalcanzables por el limite E1-04 ----------------------------
            float fMax = Valor("E1-04");
            for (int i = 1; i <= 4; i++)
            {
                string id = "d1-0" + i;
                if (Valor(id) > fMax + Epsilon)
                {
                    problemas.Add(new VfdConfigIssue(id, VfdIssueSeverity.Advertencia,
                        id + " = " + Valor(id).ToString("0.##") + " Hz supera la frecuencia maxima E1-04 = " +
                        fMax.ToString("0.#") + " Hz, asi que queda recortada a " + fMax.ToString("0.#") + " Hz."));
                }
            }

            // --- 5. Seguridad ----------------------------------------------------------------
            if (ValorEntero("L1-01") == 0)
            {
                problemas.Add(new VfdConfigIssue("L1-01", VfdIssueSeverity.Advertencia,
                    "Proteccion termica del motor deshabilitada (L1-01 = 0). El variador no protegera " +
                    "al motor contra sobrecarga. Debe ser 1."));
            }

            // --- 6. Desviaciones del resto de la tabla de referencia -------------------------
            foreach (var kv in referencia)
            {
                // Ya cubiertos arriba con un mensaje especifico.
                if (kv.Key == "b1-01" || kv.Key == "b1-02" || kv.Key == "L1-01" ||
                    kv.Key == "H1-01" || kv.Key == "H1-04" ||
                    kv.Key == "H1-05" || kv.Key == "H1-06") continue;

                float actual = Valor(kv.Key);
                if (Mathf.Abs(actual - kv.Value) <= 0.005f) continue;

                VfdParameterDef def = VfdParameterTable.Get(kv.Key);
                problemas.Add(new VfdConfigIssue(kv.Key, VfdIssueSeverity.Info,
                    def.Descripcion + ": " + def.Formatear(actual) + " " + def.Unidad +
                    " (se esperaba " + def.Formatear(kv.Value) + " " + def.Unidad + ")."));
            }

            return problemas;
        }

        /// <summary>¿La configuración permite completar la práctica sin errores graves?</summary>
        public bool ConfiguracionEsOperable()
        {
            var problemas = ValidarConfiguracion();
            for (int i = 0; i < problemas.Count; i++)
                if (problemas[i].Severidad == VfdIssueSeverity.Critica)
                    return false;
            return true;
        }
    }
}
