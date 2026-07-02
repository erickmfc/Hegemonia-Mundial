using UnityEngine;
using UnityEngine.AI;

public class EnemySoldier : MonoBehaviour {
	public GameObject Parachute;
	public NavMeshAgent Agent;
	public float SoldierLifeTime;
	public Transform Anchor;

	private Transform[] _waypoints;
	private Vector3 _targetPoint;
	private Rigidbody[] _bodyPartsRB;
	private Collider[] _bodyPartsCollder;
	private Animator _animator;
	private bool _animatorHasRun;
	private bool _isdead = false;
	private int _animatorRunHash = Animator.StringToHash("Run");

	void Awake()
	{	
		if (!Agent)
			Agent = GetComponent<NavMeshAgent>();

		if(Anchor)
		{	
			_bodyPartsRB = Anchor.GetComponentsInChildren<Rigidbody>();
			_bodyPartsCollder = Anchor.GetComponentsInChildren<Collider>();
		}
		
		_animator = GetComponent<Animator>();
		_animatorHasRun = HasAnimatorBool(_animator, _animatorRunHash);
	}

	void Start()
	{	
		if(Anchor)
		{	
			if(_bodyPartsRB != null && _bodyPartsCollder != null)
			{
				for(int i = 0; i < _bodyPartsRB.Length && i < _bodyPartsCollder.Length; i ++)
				{
					if(_bodyPartsRB[i] != null)
						_bodyPartsRB[i].isKinematic = true;
					if(_bodyPartsCollder[i] != null)
						_bodyPartsCollder[i].enabled = false;
				}
			}		
		}

		if(Manager.GetInstance() != null)
			_waypoints = Manager.GetInstance().Waypoints1;
		else
			Debug.LogWarning("[EnemySoldier] Manager.GetInstance() retornou null.");
		
		if(_waypoints == null || _waypoints.Length == 0)
			Debug.LogWarning("[EnemySoldier] Nenhum waypoint configurado.");

		//Invoke("Dead", 1);
		Invoke("Destroy", SoldierLifeTime);
	}

	void Update()
	{
		if (_isdead || !Agent)
			return;

		if (_animatorHasRun && _animator)
			_animator.SetBool(_animatorRunHash, Agent.enabled && Agent.velocity.sqrMagnitude > 0.01f);

		if (Agent.enabled && Agent.isOnNavMesh)
		{
			Vector3 desiredVelocity = Agent.desiredVelocity;
			if (desiredVelocity.sqrMagnitude > 0.0001f)
			{
				Quaternion newRot = Quaternion.LookRotation(desiredVelocity);
				transform.rotation = Quaternion.Slerp(transform.rotation, newRot, Time.deltaTime * 5.0f);
			}
		}
		
		if(Agent.enabled == true)
		{	
			
			if(Vector3.Distance(transform.position, _targetPoint) < Agent.stoppingDistance)
			{
				DefinirDestinoAleatorio();
			}

			if (Agent.isPathStale || 
			!Agent.hasPath   ||
			Agent.pathStatus!=NavMeshPathStatus.PathComplete) 
			{
				DefinirDestinoAleatorio();
			}
		}
	}

	private static bool HasAnimatorBool(Animator animator, int parameterHash)
	{
		if (!animator || animator.runtimeAnimatorController == null)
			return false;

		AnimatorControllerParameter[] parameters = animator.parameters;
		for (int i = 0; i < parameters.Length; i++)
		{
			if (parameters[i].nameHash == parameterHash && parameters[i].type == AnimatorControllerParameterType.Bool)
				return true;
		}

		return false;
	}

	
	void OnCollisionEnter(Collision collision)
	{	
		if(_isdead) return;

		if(LayerMask.LayerToName(collision.gameObject.layer) == "Projectile")
		{	
			Dead();
		}

		//Debug.Log(LayerMask.LayerToName(collision.gameObject.layer));
		if(LayerMask.LayerToName(collision.gameObject.layer) == "Ground")
		{
			if (Parachute)
				Parachute.SetActive(false);
			if (Agent)
				Agent.enabled = true;
			if (_animator)
				_animator.applyRootMotion = true;
			NavAgentControl(true, false);
			DefinirDestinoAleatorio();
		}
	}

	private bool DefinirDestinoAleatorio()
	{
		if (!Agent || !Agent.enabled || !Agent.isOnNavMesh || _waypoints == null || _waypoints.Length == 0)
			return false;

		Transform waypoint = _waypoints[Random.Range(0, _waypoints.Length)];
		if (!waypoint)
			return false;

		RandomPoint(waypoint.position, 2f, out _targetPoint);
		return true;
	}

	

	//calculate random point for movement on navigation mesh
    private void RandomPoint(Vector3 center, float range, out Vector3 result)
	{
		//clear previous target point
		result = Vector3.zero;
		
		//try to find a valid point on the navmesh with an upper limit (10 times)
		for (int i = 0; i < 10; i++)
		{
			//find a point in the movement radius
			Vector3 randomPoint = center + (Vector3)Random.insideUnitCircle * range;
			randomPoint.y = 0;
			NavMeshHit hit;

			//if the point found is a valid target point, set it and continue
			if (NavMesh.SamplePosition(randomPoint, out hit, 2f, NavMesh.AllAreas)) 
			{
				result = hit.position;
				break;
			}
		}
		
		//set the target point as the new destination
		if (Agent && Agent.enabled && Agent.isOnNavMesh)
			Agent.SetDestination(result);
	}

	public void NavAgentControl( bool positionUpdate, bool rotationUpdate )
	{
		if (Agent)
		{
			Agent.updatePosition = positionUpdate;
			Agent.updateRotation = rotationUpdate;
		}
	}

	void Dead()
	{
		_isdead = true;
		if(Parachute.activeInHierarchy)
			Parachute.SetActive(false);
		if(Anchor)
			Anchor.transform.SetParent(null);
		if(Agent.enabled == true)
			Agent.isStopped = true;
		
		_animator.enabled = false;
		if(Anchor)
		{
			for(int i = 0; i < _bodyPartsRB.Length; i ++)
			{
				_bodyPartsRB[i].isKinematic = false;
				_bodyPartsCollder[i].enabled = true;
			}
		}

		Destroy();
			
			
	}

	void Destroy()
	{
		Destroy(gameObject);
	}

	

}
