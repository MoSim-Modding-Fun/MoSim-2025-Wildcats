using Games.Reefscape.Enums;
using Games.Reefscape.GamePieceSystem;
using Games.Reefscape.Robots;
using MoSimCore.BaseClasses.GameManagement;
using MoSimCore.Enums;
using MoSimLib;
using Prefabs.Reefscape.Robots.Mods.Wildcats._9483;
using RobotFramework.Components;
using RobotFramework.Controllers.GamePieceSystem;
using RobotFramework.Controllers.PidSystems;
using RobotFramework.Enums;
using RobotFramework.GamePieceSystem;
using UnityEngine;

namespace Prefabs.Reefscape.Robots.Mods.Wildcats._9483
{
    public class Wildcats: ReefscapeRobotBase
    {
        #region Serialized Fields and Variables
        
        [Header("Components")]
        [SerializeField] private GenericElevator elevator;
        [SerializeField] private GenericJoint intakePivot, climber, climberJointLeft, climberJointRight, algaeDescore;
        
        [Header("PIDS")]
        [SerializeField] private PidConstants intakePivotPID, climberPID, climberJointsPID, algaeDescorePID;

        [Header("Setpoints")]
        [SerializeField] private WildcatsSetpoint stow, intake, l1, l2, l3, l4;
        [SerializeField] private WildcatsSetpoint lowDescore, highDescore;
        [SerializeField] private float climberStow, climberClimb;
        [SerializeField] private float climberJointStow, climberJointClimb, climberJointClimbed;
        
        [Header("Intake Components")]
        [SerializeField] private ReefscapeGamePieceIntake coralIntake;
        
        [Header("Game Piece States")]
        [SerializeField] private GamePieceState coralStowState, coralIntakeState;
        
        [Header("Robot Audio")]
        [SerializeField] private AudioSource rollerSource;
        [SerializeField] private AudioClip intakeClip;
        
        [Header("Funnel Close Audio")]
        [SerializeField] private AudioSource funnelCloseSource;
        [SerializeField] private AudioClip funnelCloseAudio;
        [SerializeField] private BoxCollider coralTrigger;
        private OverlapBoxBounds soundDetector;
        
        
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;

        private float _elevatorTargetHeight, _intakeTargetAngle, _climberTargetAngle, _climberJointsTargetAngle, _algaeDescoreTargetAngle;

        private LayerMask coralMask;
        private bool canClack;

        private bool _atSetpoint = true;
        
        #endregion
        
        protected override void Start()
        {
            base.Start();
            
            intakePivot.SetPid(intakePivotPID);
            climber.SetPid(climberPID);
            climberJointLeft.SetPid(climberJointsPID);
            climberJointRight.SetPid(climberJointsPID);
            algaeDescore.SetPid(algaeDescorePID);

            _elevatorTargetHeight = stow.elevatorHeight;
            _intakeTargetAngle = stow.intakeAngle;
            _climberTargetAngle = 0;
            _climberJointsTargetAngle = 0;
            _algaeDescoreTargetAngle = 0;
            
            RobotGamePieceController.SetPreload(coralStowState);
            _coralController = RobotGamePieceController.GetPieceByName(nameof(ReefscapeGamePieceType.Coral));

            _coralController.gamePieceStates = new[]
            {
                coralStowState
            };
            _coralController.intakes.Add(coralIntake);
            
            rollerSource.clip = intakeClip;
            rollerSource.loop = true;
            rollerSource.Stop();
            
            funnelCloseSource.clip = funnelCloseAudio;
            funnelCloseSource.loop = false;
            funnelCloseSource.Stop();

            soundDetector = new OverlapBoxBounds(coralTrigger);

            coralMask = LayerMask.GetMask("Coral");
            canClack = true;
        }

        private void LateUpdate()
        {
            intakePivot.UpdatePid(intakePivotPID);
            climber.UpdatePid(climberPID);
            climberJointLeft.UpdatePid(climberJointsPID);
            climberJointRight.UpdatePid(climberJointsPID);
            algaeDescore.UpdatePid(algaeDescorePID);
        }

        private void FixedUpdate()
        {
            bool hasCoral = _coralController.HasPiece();
            
            _coralController.SetTargetState(coralStowState);
            //_coralController.RequestIntake(coralIntake, SuperstructureAtSetpoint(intake) && IntakeAction.IsPressed() && !hasCoral);
            
            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow: SetSetpoint(stow); break;
                case ReefscapeSetpoints.Intake: SetSetpoint(intake); break;
                
                case ReefscapeSetpoints.Processor: SetState(ReefscapeSetpoints.Stow); break;
                case ReefscapeSetpoints.Stack: SetState(ReefscapeSetpoints.Stow); break;
                case ReefscapeSetpoints.Barge: SetState(ReefscapeSetpoints.Stow); break;
                
                case ReefscapeSetpoints.LowAlgae: SetSetpoint(lowDescore); break;
                case ReefscapeSetpoints.HighAlgae: SetSetpoint(highDescore); break;
                
                case ReefscapeSetpoints.L1: if (_coralController.atTarget) SetSetpoint(l1); break;
                case ReefscapeSetpoints.L2: if (_coralController.atTarget) SetSetpoint(l2); break;
                case ReefscapeSetpoints.L3: if (_coralController.atTarget) SetSetpoint(l3); break;
                case ReefscapeSetpoints.L4: if (_coralController.atTarget) SetSetpoint(l4); break;
                
                case ReefscapeSetpoints.Climb: SetSetpoint(stow); SetClimberAngle(climberStow, climberJointClimb); break;
                case ReefscapeSetpoints.Climbed: SetClimberAngle(climberClimb, climberJointClimbed); break;
                
                case ReefscapeSetpoints.Place: PlacePiece(); break;
            }

            if (CurrentSetpoint != ReefscapeSetpoints.Climb && CurrentSetpoint != ReefscapeSetpoints.Climbed)
            {
                SetClimberAngle(climberStow, climberJointStow);
            }
            
            UpdateSetpoints();
            UpdateAudio();
        }

        #region Actuators & Setpoints
        
        private void SetSetpoint(WildcatsSetpoint setpoint)
        {
            _elevatorTargetHeight = setpoint.elevatorHeight;
            _intakeTargetAngle = setpoint.intakeAngle;
        }

        private void SetClimberAngle(float climberAngle, float climberJointsAngle)
        {
            _climberTargetAngle = climberAngle;
            _climberJointsTargetAngle = climberJointsAngle;
        }

        private void SetAlgaeDescoreAngle(float algaeDescoreAngle)
        {
            _algaeDescoreTargetAngle = algaeDescoreAngle;
        }

        private void UpdateSetpoints()
        {
            elevator.SetTarget(_elevatorTargetHeight);
            intakePivot.SetTargetAngle(_intakeTargetAngle).withAxis(JointAxis.X);
            climber.SetTargetAngle(_climberTargetAngle).withAxis(JointAxis.X);
            climberJointLeft.SetTargetAngle(-_climberJointsTargetAngle).withAxis(JointAxis.X);
            climberJointRight.SetTargetAngle(_climberJointsTargetAngle).withAxis(JointAxis.X);
            algaeDescore.SetTargetAngle(_algaeDescoreTargetAngle).withAxis(JointAxis.X);

        }
        
        #endregion
        

        #region Logic Helpers

        private bool ElevatorAtSetpoint(WildcatsSetpoint targetSetpoint)
        {
            bool elevatorAtSetpoint = Utils.InRange(elevator.GetElevatorHeight(), targetSetpoint.elevatorHeight, 5f);

            return elevatorAtSetpoint;
        }
        
        private bool IntakeAtSetpoint(WildcatsSetpoint targetSetpoint)
        {
            bool intakeAtSetpoint = Utils.InAngularRange(intakePivot.GetSingleAxisAngle(JointAxis.X), targetSetpoint.intakeAngle, 5f);

            return intakeAtSetpoint;
        }

        private bool SuperstructureAtSetpoint(WildcatsSetpoint targetSetpoint)
        {
            return IntakeAtSetpoint(targetSetpoint) && ElevatorAtSetpoint(targetSetpoint);
        }

        private void UpdateAudio()
        {
            if (BaseGameManager.Instance.RobotState == RobotState.Disabled)
            {
                if (rollerSource.isPlaying)
                {
                    rollerSource.Stop();
                }

                return;
            }

            if (((IntakeAction.IsPressed() && !_coralController.HasPiece() && !_coralController.HasPiece()) ||
                 OuttakeAction.IsPressed()) &&
                !rollerSource.isPlaying)
            {
                rollerSource.Play();
            }
            else if (!IntakeAction.IsPressed() && !OuttakeAction.IsPressed() && rollerSource.isPlaying)
            {
                rollerSource.Stop();
            }
            else if (IntakeAction.IsPressed() && (_coralController.HasPiece()))
            {
                rollerSource.Stop();
            }

            var a = soundDetector.OverlapBox(coralMask);
            if (a.Length > 0)
            {
                if (canClack && !funnelCloseSource.isPlaying)
                {
                    funnelCloseSource.Play();
                    canClack = false;
                }
            }
            else
            {
                canClack = true;
            }
        }

        private void PlacePiece()
        {
            _coralController.ReleaseGamePieceWithContinuedForce(new Vector3(0, 0, 4), 0.6f, 1f);
        }
        
        #endregion
    }
}