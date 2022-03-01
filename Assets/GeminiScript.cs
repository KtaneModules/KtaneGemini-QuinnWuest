using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Collections.Generic;

public class GeminiScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable[] ButtonSels;
    public KMSelectable GoSel;
    public TextMesh[] ScreenTexts;
    public TextMesh TimerText;
    public TextMesh IdText;
    public string ModuleName;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private int[] _startingNums = new int[3];
    private int[] _currentNums = new int[3];
    private string SerialNumber;
    private int[][] _snIxs = new int[3][] { new int[3], new int[3], new int[3] };
    private int[][] _snThreeDigits = new int[3][] { new int[3], new int[3], new int[3] };
    private int[] _functionOffsets = new int[3];

    private Coroutine _cycleText;
    private bool _inputStarted;
    private string _inputString = "---------";
    private int _timer;
    private bool _timerStarted;
    private int[] _inputNums = new int[3];

    private class GeminiInfo
    {
        public List<GeminiScript> CastorModules = new List<GeminiScript>();
        public List<GeminiScript> PolluxModules = new List<GeminiScript>();
    }
    private static readonly Dictionary<string, GeminiInfo> _infos = new Dictionary<string, GeminiInfo>();
    private GeminiInfo _info;
    private GeminiScript _partner;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);
        GoSel.OnInteract += GoPress;

        SerialNumber = BombInfo.GetSerialNumber();
        if (!_infos.ContainsKey(SerialNumber))
            _infos[SerialNumber] = new GeminiInfo();
        _info = _infos[SerialNumber];
        if (ModuleName == "Castor")
            _info.CastorModules.Add(this);
        if (ModuleName == "Pollux")
            _info.PolluxModules.Add(this);
        StartCoroutine(Init());

        for (int i = 0; i < _startingNums.Length; i++)
        {
            _startingNums[i] = Rnd.Range(0, 1000);
            _currentNums[i] = _startingNums[i];
            ScreenTexts[i].text = _currentNums[i].ToString("000");
        }
        Debug.LogFormat("[{0} #{1}] Starting numbers: {2}", ModuleName, _moduleId, _startingNums.Join(", "));
        var snArr = SerialNumber.Select(i => i >= '0' && i <= '9' ? i - '0' : i - 'A' + 1).ToArray();
        newOffset:
        for (int i = 0; i < _functionOffsets.Length; i++)
        {
            _snIxs[i] = Enumerable.Range(0, 6).ToArray().Shuffle().Take(3).ToArray();
            for (int j = 0; j < 3; j++)
                _snThreeDigits[i][j] = snArr[_snIxs[i][j]];
            _functionOffsets[i] = _snThreeDigits[i][0] * _snThreeDigits[i][1] + _snThreeDigits[i][2];
        }
        if (_functionOffsets.Distinct().Count() != 3 || _functionOffsets.Contains(0))
            goto newOffset;
        for (int i = 0; i < 3; i++)
        {
            Debug.LogFormat("[{0} #{1}] Function {2} serial number positions: {3}, {4}, {5}.", ModuleName, _moduleId, "ABC"[i], _snIxs[i][0] + 1, _snIxs[i][1] + 1, _snIxs[i][2] + 1);
            Debug.LogFormat("[{0} #{1}] Function {2} value: n {3} {4}.", ModuleName, _moduleId, "ABC"[i], ModuleName == "Castor" ? "+" : "-", _functionOffsets[i]);
        }
        _cycleText = StartCoroutine(CycleText());
    }

    private IEnumerator Init()
    {
        yield return null;
        var ix = (ModuleName == "Castor" ? _info.CastorModules : _info.PolluxModules).IndexOf(this);
        var pList = ModuleName == "Pollux" ? _info.CastorModules : _info.PolluxModules;
        _partner = pList.Count > ix ? pList[ix] : null;
        var idTxt = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[ix / 26].ToString() + "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[ix % 26].ToString();
        IdText.text = idTxt;
        Debug.LogFormat("[{0} #{1}] This module's ID is {2}", ModuleName, _moduleId, idTxt);
        _timer = Rnd.Range(30, 71);
        if (_partner != null)
        {
            Debug.LogFormat("[{0} #{1}] This module has a partner!", ModuleName, _moduleId);
            if (_partner._timer != 0)
                while (Math.Abs(_timer - _partner._timer) < 20)
                    _timer = Rnd.Range(20, 81);
        }
        else
        {
            Debug.LogFormat("[{0} #{1}] This module does not have a partner.", ModuleName, _moduleId);
        }
        Debug.LogFormat("[{0} #{1}] Possible input: {2}", ModuleName, _moduleId, _functionOffsets.Select(i => ModuleName == "Pollux" ? i * _timer % 1000 : (1000 - i) * _timer % 1000).Join(" "));
        TimerText.text = _timer.ToString();
    }

    private KMSelectable.OnInteractHandler ButtonPress(int btn)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ButtonSels[btn].transform);
            ButtonSels[btn].AddInteractionPunch(0.5f);
            if (_moduleSolved || _timerStarted)
                return false;
            if (!_inputStarted)
                _inputStarted = true;
            _inputString = _inputString.Substring(1) + btn;
            for (int i = 0; i < ScreenTexts.Length; i++)
                ScreenTexts[i].text = _inputString.Substring(i * 3, 3);
            return false;
        };
    }

    private bool GoPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, GoSel.transform);
        GoSel.AddInteractionPunch(0.5f);
        if (_moduleSolved || _timerStarted || !_inputStarted)
            return false;
        for (int i = 0; i < ScreenTexts.Length; i++)
        {
            if (!int.TryParse(ScreenTexts[i].text, out _inputNums[i]) || _inputNums[i] < 0)
            {
                Module.HandleStrike();
                Debug.LogFormat("[{0} #{1}] Attempted to start the timer before entering all digits. Strike.", ModuleName, _moduleId);
                _inputStarted = false;
                return false;
            }
            _timerStarted = true;
        }
        return false;
    }

    private IEnumerator CycleText()
    {
        while (!_moduleSolved)
        {
            if (!_inputStarted)
            {
                for (int i = 0; i < _currentNums.Length; i++)
                {
                    _currentNums[i] = FunctionEncode(_currentNums[i], i);
                    ScreenTexts[i].text = _currentNums[i].ToString("000");
                }
            }
            else if (_timerStarted)
            {
                if (_timer == 0)
                {
                    yield return null;
                    if (!CheckAnswer())
                    {
                        yield return null;
                        _timerStarted = false;
                        _inputStarted = false;
                        _timer = Rnd.Range(30, 71);
                        TimerText.text = _timer.ToString();
                    }
                }
                else
                {
                    for (int i = 0; i < _currentNums.Length; i++)
                    {
                        _inputNums[i] = FunctionEncode(_inputNums[i], i);
                        ScreenTexts[i].text = _inputNums[i].ToString("000");
                    }
                    _timer--;
                    TimerText.text = _timer.ToString();
                }
            }
            int time = (int)BombInfo.GetTime();
            while (time == (int)BombInfo.GetTime())
                yield return null;
            if (_partner != null && _partner._timer == 0 && _timer != 0)
            {
                yield return null;
                if (_partner._moduleSolved)
                {
                    Module.HandleStrike();
                    Debug.LogFormat("[{0} #{1}] This module's partner solved prematurely. Strike.", ModuleName, _moduleId);
                }
                else
                    Debug.LogFormat("[{0} #{1}] Stopping the timer on this module because its partner's timer ran out and struck.", ModuleName, _moduleId);
                Debug.LogFormat("[{0} #{1}] Regenerating...", ModuleName, _moduleId);
                _timerStarted = false;
                _inputStarted = false;
                _partner = null;
                _timer = Rnd.Range(30, 71);
                Debug.LogFormat("[{0} #{1}] Possible input: {2}", ModuleName, _moduleId, _functionOffsets.Select(i => ModuleName == "Pollux" ? i * _timer % 1000 : (1000 - i) * _timer % 1000).Join(" "));
                TimerText.text = _timer.ToString();
            }
        }
    }

    private bool CheckAnswer()
    {
        if (_inputNums.Distinct().Count() == 1)
        {
            _moduleSolved = true;
            Module.HandlePass();
            if (_cycleText != null)
                StopCoroutine(_cycleText);
            Debug.LogFormat("[{0} #{1}] All three numbers are equal the end of the timer. Module solved.", ModuleName, _moduleId);
            return true;
        }
        else
        {
            Module.HandleStrike();
            Debug.LogFormat("[{0} #{1}] The three numbers ({2}) are not equal at the end of the timer. Strike.", ModuleName, _moduleId, _inputNums.Join(", "));
            return false;
        }
    }

    private int FunctionEncode(int num, int ix)
    {
        int val = num;
        switch (ModuleName)
        {
            case "Castor":
                val += _functionOffsets[ix];
                break;
            case "Pollux":
                val -= _functionOffsets[ix];
                break;
        }
        return (val + 1000) % 1000;
    }
}
