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
        
        [SerializeField] private PidConstants intakePivotPID, climberPID, climberJointLeftPID, climberJointRightPID, algaeDescorePID;
        
        [Header("Intake Things")]
        
        [SerializeField] private GenericRoller topRoller, leftRoller, rightRoller;
        [SerializeField] private Transform leftSensor, rightSensor;

        [Header("Setpoints")]
        
        [SerializeField] private WildcatsSetpoint stow, intake, l1, l2, l3, l4;
        [SerializeField] private WildcatsSetpoint lowDescore, highDescore;
        
        [Header("Climb Setpoints")]
        
        [SerializeField] private WildcatsClimbSetpoint climbStow, prep, climb;
        
        [Header("Intake Components")]
        
        [SerializeField] private ReefscapeGamePieceIntake coralIntake;
        
        [Header("Game Piece States")]
        
        [SerializeField] private GamePieceState coralIntakeState, coralTransferState1, coralTransferState2, coralTransferState3, coralTransferState4, coralStowState;
        
        [Header("Robot Audio")]
        
        [SerializeField] private AudioSource rollerSource;
        [SerializeField] private AudioClip intakeClip;
        
        [Header("Funnel Close Audio")]
        
        [SerializeField] private AudioSource funnelCloseSource;
        [SerializeField] private AudioClip funnelCloseAudio;
        [SerializeField] private BoxCollider coralTrigger;
        private OverlapBoxBounds soundDetector;
        
        private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode _coralController;

        private float _elevatorTargetHeight, _intakeTargetAngle, _climberTargetAngle, _climberLeftPincerTarget, _climberRightPincerTarget, _algaeDescoreTargetAngle;

        private LayerMask coralMask;
        private bool canClack;
        
        private ReefscapeAutoAlign align;
        
        #endregion
        
        protected override void Start()
        {
            base.Start();
            
            intakePivot.SetPid(intakePivotPID);
            climber.SetPid(climberPID);
            climberJointLeft.SetPid(climberJointLeftPID);
            climberJointRight.SetPid(climberJointRightPID);
            algaeDescore.SetPid(algaeDescorePID);

            _elevatorTargetHeight = stow.elevatorHeight;
            _intakeTargetAngle = stow.intakeAngle;
            _climberTargetAngle = climbStow.elevatorAngle;
            _climberLeftPincerTarget = climbStow.leftPincerAngle;
            _climberRightPincerTarget = climbStow.rightPincerAngle;
            _algaeDescoreTargetAngle = 0;
            
            RobotGamePieceController.SetPreload(coralStowState);
            _coralController = RobotGamePieceController.GetPieceByName(nameof(ReefscapeGamePieceType.Coral));

            _coralController.gamePieceStates = new[]
            {
                coralStowState,
                coralTransferState1,
                coralTransferState2,
                coralTransferState3,
                coralTransferState4,
                coralIntakeState
            };
            _coralController.intakes.Add(coralIntake);
            
            align = gameObject.GetComponent<ReefscapeAutoAlign>();
            
            rollerSource.clip = intakeClip;
            rollerSource.loop = true;
            rollerSource.Stop();
            
            funnelCloseSource.clip = funnelCloseAudio;
            funnelCloseSource.loop = false;
            funnelCloseSource.Stop();

            soundDetector = new OverlapBoxBounds(coralTrigger);
            canClack = true;
        }

        private void LateUpdate()
        {
            intakePivot.UpdatePid(intakePivotPID);
            climber.UpdatePid(climberPID);
            climberJointLeft.UpdatePid(climberJointLeftPID);
            climberJointRight.UpdatePid(climberJointRightPID);
            algaeDescore.UpdatePid(algaeDescorePID);
        }

        private void FixedUpdate()
        {
            AutoAlignLogic();
            
            bool hasCoral = _coralController.HasPiece();
            bool eeHasCoral = _coralController.currentStateNum == coralStowState.stateNum && _coralController.atTarget;
            
            _coralController.RequestIntake(coralIntake, SuperstructureAtSetpoint(intake) && IntakeAction.IsPressed() && !hasCoral);
            
            if (eeHasCoral)
            {
                switch (CurrentSetpoint)
                {
                    case ReefscapeSetpoints.L4: 
                        SetSetpoint(l4); 
                        break;
                    
                    case ReefscapeSetpoints.L3: 
                        SetSetpoint(l3); 
                        break;
                    
                    case ReefscapeSetpoints.L2: 
                        SetSetpoint(l2); 
                        break;
                    
                    case ReefscapeSetpoints.L1: 
                        SetSetpoint(l1); 
                        break;
                }
            }
            
            AnimateCoralHandoff();
            
            switch (CurrentSetpoint)
            {
                case ReefscapeSetpoints.Stow: 
                    SetSetpoint(stow); 
                    SetAlgaeDescoreAngle(0); 
                    break;
                
                case ReefscapeSetpoints.Intake:
                    if (!hasCoral) _coralController.SetTargetState(coralIntakeState);
                    SetSetpoint(intake);
                    break;
                
                
                case ReefscapeSetpoints.LowAlgae: 
                    SetSetpoint(lowDescore); 
                    SetAlgaeDescoreAngle(130); 
                    if(IntakeAction.IsPressed()) SetState(ReefscapeSetpoints.Intake); 
                    break;
                
                case ReefscapeSetpoints.HighAlgae: 
                    SetSetpoint(highDescore);
                    SetAlgaeDescoreAngle(110); 
                    if(IntakeAction.IsPressed()) SetState(ReefscapeSetpoints.Intake); 
                    break;
                
                case ReefscapeSetpoints.Climb: 
                    SetSetpoint(intake); 
                    SetClimberAngle(SuperstructureAtSetpoint(intake) ? prep : climbStow); 
                    break;
                
                case ReefscapeSetpoints.Climbed: 
                    SetSetpoint(intake); 
                    SetClimberAngle(climb); 
                    break;
                
                case ReefscapeSetpoints.Place: 
                    PlacePiece(); 
                    break;
                
                case ReefscapeSetpoints.RobotSpecial: 
                    SetAlgaeDescoreAngle(0); 
                    SetState(ReefscapeSetpoints.Stow); 
                    break;
                case ReefscapeSetpoints.Processor: 
                    SetAlgaeDescoreAngle(0); 
                    SetState(ReefscapeSetpoints.Stow); 
                    break;
                case ReefscapeSetpoints.Stack: 
                    SetAlgaeDescoreAngle(0); 
                    SetState(ReefscapeSetpoints.Stow); 
                    break;
                case ReefscapeSetpoints.Barge: 
                    SetAlgaeDescoreAngle(0); 
                    SetState(ReefscapeSetpoints.Stow); 
                    break;
            }

            if (CurrentSetpoint != ReefscapeSetpoints.Climb && CurrentSetpoint != ReefscapeSetpoints.Climbed &&
                LastSetpoint != ReefscapeSetpoints.Climb && LastSetpoint != ReefscapeSetpoints.Climbed)
            {
                SetClimberAngle(climbStow);
            }
            
            _coralController.MoveIntake(coralIntake, coralIntakeState.stateTarget);
            if (!leftRoller.gameObject.activeSelf)
            {
                leftRoller.gameObject.SetActive(true);
                rightRoller.gameObject.SetActive(true);
            }

            var rayDirection = coralIntakeState.stateTarget.forward;
            var distance = 0.0254f * 5f;
            var coralMask = LayerMask.GetMask("Coral");
            var coralRight = Physics.Raycast(rightSensor.position, rayDirection, distance, coralMask);
            var coralLeft = Physics.Raycast(leftSensor.position, rayDirection, distance, coralMask);

            if (IntakeAction.IsPressed() && CurrentSetpoint != ReefscapeSetpoints.LowAlgae && CurrentSetpoint != ReefscapeSetpoints.HighAlgae)
            {
                if (coralRight && coralLeft)
                {
                    leftRoller.ChangeAngularVelocity(8000);
                    rightRoller.ChangeAngularVelocity(8000);
                }
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

        private void SetClimberAngle(WildcatsClimbSetpoint setpoint)
        {
            _climberTargetAngle = setpoint.elevatorAngle;
            _climberLeftPincerTarget = setpoint.leftPincerAngle;
            _climberRightPincerTarget = setpoint.rightPincerAngle;
        }

        private void SetAlgaeDescoreAngle(float algaeDescoreAngle)
        {
            _algaeDescoreTargetAngle = algaeDescoreAngle;
        }

        private void UpdateSetpoints()
        {
            elevator.SetTarget(_elevatorTargetHeight);
            intakePivot.SetTargetAngle(_intakeTargetAngle).withAxis(JointAxis.X);
            climber.SetTargetAngle(_climberTargetAngle).withAxis(JointAxis.X).noWrap(-90);
            climberJointLeft.SetTargetAngle(_climberLeftPincerTarget).withAxis(JointAxis.Y).noWrap(90);
            climberJointRight.SetTargetAngle(_climberRightPincerTarget).withAxis(JointAxis.Y).noWrap(-90);
            algaeDescore.SetTargetAngle(_algaeDescoreTargetAngle).withAxis(JointAxis.X).useCustomStartingOffset(-30);

        }
        
        #endregion
        

        #region Logic Helpers

        private bool CoralAtState(GamePieceState state)
        {
            return _coralController.atTarget && _coralController.currentStateNum == state.stateNum;
        }

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

        private bool IsCoralSetpoint()
        {
            return CurrentSetpoint == ReefscapeSetpoints.L4 ||
                   CurrentSetpoint == ReefscapeSetpoints.L3 ||
                   CurrentSetpoint == ReefscapeSetpoints.L2 ||
                   CurrentSetpoint == ReefscapeSetpoints.L1 ||
                   LastSetpoint == ReefscapeSetpoints.L4 ||
                   LastSetpoint == ReefscapeSetpoints.L3 ||
                   LastSetpoint == ReefscapeSetpoints.L2 ||
                   LastSetpoint == ReefscapeSetpoints.L1;
        }

        private void PlacePiece()
        {
            if (!IsCoralSetpoint()) return;

            if (LastSetpoint == ReefscapeSetpoints.L4)
            {
                _coralController.ReleaseGamePieceWithContinuedForce(new Vector3(0, 0, 4), 0.7f, 0.5f);
                return;
            }
            else if (LastSetpoint == ReefscapeSetpoints.L1)
            {
                
                _coralController.ReleaseGamePieceWithContinuedForce(new Vector3(0, 0, 2), 0.2f, 0.5f);
                return;
            }
            _coralController.ReleaseGamePieceWithForce(new Vector3(0, 0, 4));
        }

        private void AnimateCoralHandoff()
        {
            if (SuperstructureAtSetpoint(stow) && CoralAtState(coralIntakeState))
            {
                _coralController.SetTargetState(coralTransferState1);
            } 
            else if (CoralAtState(coralTransferState1))
            {
                _coralController.SetTargetState(coralTransferState2);
            } 
            else if (CoralAtState(coralTransferState2))
            {
                _coralController.SetTargetState(coralTransferState3);
            } 
            else if (CoralAtState(coralTransferState3))
            {
                _coralController.SetTargetState(coralTransferState4);
            } 
            else if (CoralAtState(coralTransferState4))
            {
                _coralController.SetTargetState(coralStowState);
            }
        }

        private void AutoAlignLogic()
        {
            if (CurrentSetpoint == ReefscapeSetpoints.L4 ||
                LastSetpoint == ReefscapeSetpoints.L4)
            {
                align.offset = new Vector3(0, 0, 10.5f);
            }
            else
            {
                align.offset = new Vector3(0, 0, 7);
            }
        }
        
        #endregion
    }
}