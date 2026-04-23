// ARQUIVO 4: IA_CommandExecutor.cs
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Hegemonia.AI.Master
{
    [DefaultExecutionOrder(-845)]
    public sealed class IA_CommandExecutor : MonoBehaviour
    {
        public enum CommandType
        {
            Build = 0,
            Produce = 1,
            MovePackage = 2,
            Attack = 3,
            UnloadPackage = 4
        }

        [Serializable]
        public struct AICommand
        {
            public CommandType Type;
            public string Payload;
            public Vector3 WorldPoint;
            public int Priority;
            public bool Critical;
            public float CreatedAt;
        }

        [Serializable]
        public struct CommandExecutionResult
        {
            public AICommand Command;
            public bool Success;
            public string Reason;
            public string MethodName;
            public float ElapsedMs;
        }

        private readonly List<AICommand> _queue = new List<AICommand>(128);
        private readonly Comparison<AICommand> _sorter;
        private int _teamId;
        private long _totalQueued;
        private long _totalExecuted;
        private long _totalFailed;
        private long _buildAttempts;
        private long _buildSuccesses;
        private string _lastFailureReason = string.Empty;
        private CommandExecutionResult _lastResult;

        public int PendingCount => _queue.Count;
        public long TotalQueued => _totalQueued;
        public long TotalExecuted => _totalExecuted;
        public long TotalFailed => _totalFailed;
        public long BuildAttempts => _buildAttempts;
        public long BuildSuccesses => _buildSuccesses;
        public float BuildSuccessRate => _buildAttempts <= 0 ? 1f : ((float)_buildSuccesses / _buildAttempts);
        public string LastFailureReason => _lastFailureReason;
        public CommandExecutionResult LastResult => _lastResult;

        public IA_CommandExecutor()
        {
            _sorter = CompareCommands;
        }

        public void Configure(int teamId)
        {
            _teamId = teamId;
        }

        public void QueueBuild(string itemKey, Vector3 point, int priority, bool critical)
        {
            Enqueue(new AICommand
            {
                Type = CommandType.Build,
                Payload = itemKey,
                WorldPoint = point,
                Priority = priority,
                Critical = critical,
                CreatedAt = Time.time
            });
        }

        public void QueueProduction(string itemKey, int priority, bool critical)
        {
            Enqueue(new AICommand
            {
                Type = CommandType.Produce,
                Payload = itemKey,
                WorldPoint = Vector3.zero,
                Priority = priority,
                Critical = critical,
                CreatedAt = Time.time
            });
        }

        public void QueueMovePackage(string packageTag, Vector3 point, int priority, bool critical)
        {
            Enqueue(new AICommand
            {
                Type = CommandType.MovePackage,
                Payload = packageTag,
                WorldPoint = point,
                Priority = priority,
                Critical = critical,
                CreatedAt = Time.time
            });
        }

        public void QueueAttack(string attackTag, Vector3 point, int priority, bool critical)
        {
            Enqueue(new AICommand
            {
                Type = CommandType.Attack,
                Payload = attackTag,
                WorldPoint = point,
                Priority = priority,
                Critical = critical,
                CreatedAt = Time.time
            });
        }

        public void QueueUnloadPackage(string tag, Vector3 point, int priority, bool critical)
        {
            Enqueue(new AICommand
            {
                Type = CommandType.UnloadPackage,
                Payload = tag,
                WorldPoint = point,
                Priority = priority,
                Critical = critical,
                CreatedAt = Time.time
            });
        }

        public void ClearNonCritical()
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (!_queue[i].Critical)
                {
                    _queue.RemoveAt(i);
                }
            }
        }

        public int Flush(int maxCommands, float budgetMs, MonoBehaviour backendBridge)
        {
            if (_queue.Count == 0)
            {
                return 0;
            }

            _queue.Sort(_sorter);

            int executed = 0;
            float start = Time.realtimeSinceStartup * 1000f;
            int cap = Mathf.Min(maxCommands, _queue.Count);

            for (int i = 0; i < cap; i++)
            {
                if ((Time.realtimeSinceStartup * 1000f) - start >= budgetMs)
                {
                    break;
                }

                AICommand cmd = _queue[0];
                CommandExecutionResult result = ExecuteCommandDetailed(cmd, backendBridge);
                _queue.RemoveAt(0);
                _lastResult = result;
                if (cmd.Type == CommandType.Build)
                {
                    _buildAttempts++;
                    if (result.Success)
                    {
                        _buildSuccesses++;
                    }
                }

                if (result.Success)
                {
                    executed++;
                    _totalExecuted++;
                }
                else
                {
                    _totalFailed++;
                    _lastFailureReason = string.IsNullOrEmpty(result.Reason) ? "falha desconhecida" : result.Reason;
                }
            }

            return executed;
        }

        private void Enqueue(AICommand cmd)
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Type == cmd.Type && _queue[i].Payload == cmd.Payload)
                {
                    float d = Vector3.Distance(_queue[i].WorldPoint, cmd.WorldPoint);
                    if (d <= 48f)
                    {
                        if (cmd.Priority > _queue[i].Priority)
                        {
                            _queue[i] = cmd;
                        }
                        return;
                    }
                }
            }

            _queue.Add(cmd);
            _totalQueued++;
        }

        private CommandExecutionResult ExecuteCommandDetailed(AICommand cmd, MonoBehaviour backendBridge)
        {
            float started = Time.realtimeSinceStartup * 1000f;
            CommandExecutionResult result = new CommandExecutionResult
            {
                Command = cmd,
                Success = false,
                Reason = string.Empty,
                MethodName = string.Empty,
                ElapsedMs = 0f
            };

            if (backendBridge == null)
            {
                result.Reason = "backend_bridge_nulo";
                result.ElapsedMs = (Time.realtimeSinceStartup * 1000f) - started;
                return result;
            }

            switch (cmd.Type)
            {
                case CommandType.Build:
                    if (TryInvoke(backendBridge, "TryQueueBuild", out result, _teamId, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    if (TryInvoke(backendBridge, "QueueBuild", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    TryInvoke(backendBridge, "IA_QueueBuild", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority);
                    break;

                case CommandType.Produce:
                    if (TryInvoke(backendBridge, "TryQueueProduction", out result, _teamId, cmd.Payload, cmd.Priority))
                    {
                        break;
                    }
                    if (TryInvoke(backendBridge, "QueueProduction", out result, cmd.Payload, cmd.Priority))
                    {
                        break;
                    }
                    TryInvoke(backendBridge, "IA_QueueProduction", out result, cmd.Payload, cmd.Priority);
                    break;

                case CommandType.MovePackage:
                    if (TryInvoke(backendBridge, "TryIssueMovePackage", out result, _teamId, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    if (TryInvoke(backendBridge, "IssueMovePackage", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    TryInvoke(backendBridge, "IA_IssueMovePackage", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority);
                    break;

                case CommandType.Attack:
                    if (TryInvoke(backendBridge, "TryIssueAttack", out result, _teamId, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    if (TryInvoke(backendBridge, "IssueAttack", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    TryInvoke(backendBridge, "IA_IssueAttack", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority);
                    break;

                case CommandType.UnloadPackage:
                    if (TryInvoke(backendBridge, "TryIssueUnload", out result, _teamId, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    if (TryInvoke(backendBridge, "IssueUnload", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority))
                    {
                        break;
                    }
                    TryInvoke(backendBridge, "IA_IssueUnload", out result, cmd.Payload, cmd.WorldPoint, cmd.Priority);
                    break;
            }

            if (!result.Success && string.IsNullOrEmpty(result.Reason))
            {
                result.Reason = "nenhum_metodo_compativel";
            }

            result.ElapsedMs = (Time.realtimeSinceStartup * 1000f) - started;
            return result;
        }

        private static bool TryInvoke(MonoBehaviour backend, string methodName, object[] args, out CommandExecutionResult result)
        {
            result = new CommandExecutionResult
            {
                Success = false,
                Reason = "metodo_nao_encontrado",
                MethodName = methodName
            };

            if (backend == null)
            {
                result.Reason = "backend_nulo";
                return false;
            }

            MethodInfo[] methods = backend.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo m = methods[i];
                if (m.Name != methodName)
                {
                    continue;
                }

                ParameterInfo[] pars = m.GetParameters();
                if (pars.Length != args.Length)
                {
                    continue;
                }

                try
                {
                    object invokeResult = m.Invoke(backend, args);
                    if (m.ReturnType == typeof(bool))
                    {
                        bool ok = invokeResult is bool b && b;
                        if (ok)
                        {
                            result = new CommandExecutionResult
                            {
                                Success = true,
                                Reason = "ok",
                                MethodName = methodName
                            };
                        }
                        else
                        {
                            result = new CommandExecutionResult
                            {
                                Success = false,
                                Reason = "retorno_false",
                                MethodName = methodName
                            };
                        }
                        return ok;
                    }

                    result = new CommandExecutionResult
                    {
                        Success = true,
                        Reason = "ok",
                        MethodName = methodName
                    };
                    return true;
                }
                catch (Exception ex)
                {
                    result = new CommandExecutionResult
                    {
                        Success = false,
                        Reason = "exception:" + ex.GetType().Name,
                        MethodName = methodName
                    };
                    return false;
                }
            }

            return false;
        }

        private static bool TryInvoke(MonoBehaviour backend, string methodName, out CommandExecutionResult result, params object[] args)
        {
            return TryInvoke(backend, methodName, args, out result);
        }

        private static int CompareCommands(AICommand a, AICommand b)
        {
            int p = b.Priority.CompareTo(a.Priority);
            if (p != 0)
            {
                return p;
            }
            if (a.Critical != b.Critical)
            {
                return b.Critical.CompareTo(a.Critical);
            }
            return a.CreatedAt.CompareTo(b.CreatedAt);
        }
    }
}
