using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    #region GeneralVariables
    Rigidbody2D playerRB;

    //Player Speed on ground and in air
    public int speed;
    public float maxAirSpeedToTurnAround;
    private float airSpeed;

    private float dashSpeed;

    public int maxDashes;
    public float jumpforce;
    public float wallJumpForce;
    public float wallJumpDrift;
    private bool wallJumpInfluence;

    private bool walljumpAttack;

    Animator anim;

    Transform level;

    public Vector3 respawnPos;

    public float damage;

    //The bar that represents player health and also time power
    public Slider timeBar;

    //Overlays for time/low health
    public Image timeOverlay;
    public Image lowHealthOverlay;

    //Decides whether the player should respawn or go to game over 
    public bool respawnBool;

    //The value of the time bar, will also serve as a health value
    public float timeHealthValue;

    public float normHealthDrain;

    //The multiplier on health drain while time is active
    public int timeHealthDrain;

    //The bool that represents whether Time Power is active or not
    public bool timePowerOn;

    //The first frame of time activation
    private bool timeAnim;

    //Represents whether time is currently coming into effect
    public bool timeTurningOn;

    //The variables that determine the timing the movement functions work on
    //The "initial" variables are the ones we initially set and the ones the 
    //variables will revert to once time returns to normal.
    private float secondsToWaitForDash;

    private float secondsToWaitForWallJump;

    private float secondsToWaitForJump;

    //The timing variables for hitboxes and attack animation
    //The private variables are the initial values unaffected by time
    public float hitboxTiming;

    public float attackAnimationTiming;

    //Boolean that determines whether or not player is currently in dialogue
    public bool inDialogue;

    //Determines if player can enter dialogue
    public bool canEnterDialogue;

    //Boolean that determines whether next sentence should be shown
    public bool showNextSentence;

    //Dialogue Manager to control sign post dialogue
    public DialogueManager dialogueManager;

    //Boolean that determines if we continue dialogue or exit
    private bool continueDialogue;

    //Damage and invulnerability variables. Invulnerability currently
    //works as 1/4 of invulnerabilityTime as hitstun and the
    //remainder 3/4 time as invincibility frames.
    private bool takingDamage;

    private bool takingDamageStun;

    public float invulnerabilityTime;

    //X velocity of player without input
    public float baseVelocity;

    //Platform player is standing on
    private Rigidbody2D plat;

    //Buffer Class to say whether we pause for a second or not
    [SerializeField]
    private SlowTimeBuffer timeBuffer;
    float bufferSeconds;

    
    float xInput;
    float yInput;
    float trueGrav;
    bool rightWallRiding;
    bool leftWallRiding;
    int dashes;
    public int surfacesTouching;
    float dirFacing;
    float prevDirFacing;
    public bool onGround;
    bool onRightWall;
    bool onLeftWall;
    Dictionary<Collider2D, int> touching;

    SpriteRenderer spriteRenderer;
    Color originalColor;
    int playerLayer;
    int enemyLayer;

    //Holder for original constraints
    RigidbodyConstraints2D originalConstraints;
    #endregion

    #region UnityFunctions
    // Start is called before the first frame update
    void Start()
    {
        wallJumpInfluence = false;
        plat = null;
        playerRB = GetComponent<Rigidbody2D>();
        surfacesTouching = 0;
        touching = new Dictionary<Collider2D, int>();
        dashes = maxDashes;
        dirFacing = 1;
        trueGrav = playerRB.gravityScale;
        anim = GetComponent<Animator>();
        level = transform.parent;
        baseVelocity = 0;
        respawnPos = new Vector3 (gameObject.transform.position.x, gameObject.transform.position.y, gameObject.transform.position.z);

        walljumpAttack = false;

        //Initialize min and max values for the timeBar, and set initial value
        timeBar.maxValue = 100;
        timeBar.minValue = 0;
        timeHealthValue = 100;
        timeBar.value = timeHealthValue;

        //Set speed in air
        airSpeed = 200;

        dashSpeed = 150;

        //Set Time bool
        timePowerOn = false;
        timeTurningOn = false;

        //Initialize WaitForSeconds variables
        secondsToWaitForDash = 0.15f;

        secondsToWaitForWallJump = 0.4f;

        secondsToWaitForJump = 0.03f;

        //Initialize dialogue booleans
        inDialogue = false;
        canEnterDialogue = false;
        showNextSentence = false;
        continueDialogue = false;

        takingDamage = false;
        takingDamageStun = false;
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.material.color;
        playerLayer = LayerMask.NameToLayer("Player");
        enemyLayer = LayerMask.NameToLayer("Enemy");

        //Save original constraints
        originalConstraints = playerRB.constraints;

        //Buffer Seconds
        bufferSeconds = 0.5f;
    }

    // Update is called once per frame
    void Update()
    {
        if (timeBuffer.isBuffer())
        {
            return;
        }

        if (plat != null)
        {
            baseVelocity = plat.velocity.x;

        } 

        //Let's the player skip the dialogue if they are retrying the boss fight
        if (Input.GetKeyDown(KeyCode.Return) && inDialogue)
        {
            dialogueManager.EndDialogue();
            exitDialogue();
        }

        //If we are in a dialogue state, then pressing "Return" will progress dialogue
        if (Input.GetKeyDown(KeyCode.Space) && inDialogue)
        {
            dialogueManager.DisplayNextSentence();
        }

        if (inDialogue)
        {
            if (dialogueManager.dialogueEnded)
            {
                Debug.Log("canEnterDialogue is:" + canEnterDialogue);
                exitDialogue();
            }
            else
            {
                return;
            }
        }

        //Check if transitioning
        if (StaticData.isTransitioning == true)
        {
            playerRB.velocity = Vector2.zero;
            dashes = maxDashes;
            return;
        }

        //Check if timeBar is at zero
        if (timeHealthValue <= 0)
        {
            die();
        }
        else if (timeHealthValue < 20)
        {
            lowHealthOverlay.enabled = true;
        } else
        {
            lowHealthOverlay.enabled = false;
        }

        //Decrement timeBar either naturally or because of Time Power
        if (timePowerOn)
        {
            timeOverlay.enabled = true;
            timeHealthValue -= Time.unscaledDeltaTime * timeHealthDrain;
            timeBar.value = timeHealthValue;
        }
        else
        {
            //Debug.Log("Draining normally");
            timeOverlay.enabled = false;
            timeHealthValue -= Time.unscaledDeltaTime * normHealthDrain;
            timeBar.value = timeHealthValue;
        }

        xInput = Input.GetAxisRaw("Horizontal");
        yInput = Input.GetAxisRaw("Vertical");
        
        if (state == pState.nothing || (state == pState.attacking && !walljumpAttack) || state == pState.jumping)
        {
            moveFunction();
        }
        else if ((state == pState.wallJumping || walljumpAttack) && wallJumpInfluence)
        {
            wallJumpMove();
        }

        onGround = false;
        onRightWall = false;
        onLeftWall = false;
        foreach (int side in touching.Values)
        {
            //Debug.Log(side);
            if (side == 1)
            {
                onLeftWall = true;
            }
            if (side == -1)
            {
                onRightWall = true;
            }
            if (side < 1 && side > -1)
            {
                onGround = true;
                dashes = maxDashes;
            }
        }

        // Disable controls if in damage stun
        if (!takingDamageStun)
        {

            leftWallRiding = (onLeftWall == true && onGround == false);
            rightWallRiding = (onRightWall == true && onGround == false);
            //If Space is pressed and Player is touching a wall or floor they jump/wallJump
            //If J is pressed they dash in the direction they are holding
            //If K is pressed they attack in direction of dirFacing
            if (leftWallRiding == true || rightWallRiding == true)
            {
                if (playerRB.velocity.y < 0 && xInput != 0)
                {
                    playerRB.AddForce(playerRB.velocity.normalized * -800);
                }
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    StartCoroutine(wallJump());
                }
            }
            if (Input.GetKeyDown(KeyCode.J) && dashes > 0)
            {
                StartCoroutine("Dash");
            }
            else
            {
                if ((Input.GetKeyDown(KeyCode.Space) && surfacesTouching > 0 && (state == pState.nothing || state == pState.attacking)))
                {
                    Debug.Log("trying to jump");
                    //playerRB.velocity = new Vector2(playerRB.velocity.x, 0);
                    //playerRB.AddForce(new Vector2(0, jumpforce));
                    StartCoroutine("Jump");

                }
                if (Input.GetKeyDown(KeyCode.K))
                {
                    StartCoroutine("Attack");
                }
            }
        }
        //If the velocity is in the opposite direction of the player's facing direction
        //turn the player around
        if (playerRB.velocity.x > 0 && dirFacing == -1)
        {
            dirFacing = 1;
            playerRB.transform.rotation = new Quaternion(0, 0, 0, 0);
        }
        else if (playerRB.velocity.x < 0 && dirFacing == 1)
        {
            dirFacing = -1;
            playerRB.transform.rotation = new Quaternion(0, 180, 0, 0);
        }

        //If the player activates their time ability, start Time effects
        if (Input.GetKeyDown(KeyCode.L) && !timeTurningOn)
        {
            TimePower();
            timeAnim = true;
        }

        //Set animation based on state

        if (timeAnim && anim.GetInteger("State?") != 5)
        {
            anim.SetInteger("State?", 5);
        }
        else if (state == pState.attacking && anim.GetInteger("State?") != 3)
        {
            anim.SetInteger("State?", 3);
        }
        else if (leftWallRiding || rightWallRiding)
        {
            if (anim.GetInteger("State?") != 4)
            {
                anim.SetInteger("State?", 4);
            }
        }
        else if (state == pState.nothing)
        {
            if (xInput != 0 && anim.GetInteger("State?") != 0)
            {
                anim.SetInteger("State?", 0);
            } 
            else if (xInput == 0 && anim.GetInteger("State?") != 2)
            {
                anim.SetInteger("State?", 2);
            }
        }
        else if (state == pState.jumping && anim.GetInteger("State?") != 1)
        {
            anim.SetInteger("State?", 1);
        }
        else if (state == pState.wallJumping && anim.GetInteger("State?") != 1)
        {
            anim.SetInteger("State?", 1);
        }
        else if (state == pState.dashing && anim.GetInteger("State?") != 6)
        {
            anim.SetInteger("State?", 6);
        }
        timeAnim = false;
    }
    #endregion
    

    #region CollisionFunctions
    //Update wallsTouching on entering/leaving walls
    //wallRiding becomes true when touching a wall but not floor
    void OnCollisionEnter2D(Collision2D coll)
    {
        if (coll.gameObject.CompareTag("Enemy") 
            || coll.gameObject.CompareTag("Projectile")
            || coll.gameObject.CompareTag("EnemyHurtbox"))
        {
            return;
        }
        surfacesTouching += 1;
        ContactPoint2D [] points = new ContactPoint2D[10];
        int contacts = coll.GetContacts(points);
        for (int i = 0; i < contacts; i++)
        {
            //print(points[i].normal);
            if(points[i].normal[1] > 0)
            {
                touching[coll.collider] = 0;
                if (coll.transform.tag == "Platform")
                {
                    //transform.parent = coll.transform;
                    plat = coll.gameObject.GetComponent<Rigidbody2D>();
                    baseVelocity = plat.velocity.x;
                }
            }
            if (points[i].normal[0] == 1)
            {
                touching[coll.collider] = 1;
                if (playerRB.velocity.y < 0)
                {
                    playerRB.velocity = new Vector2(playerRB.velocity.x, 0);
                }
            }
            if (points[i].normal[0] == -1)
            {
                touching[coll.collider] = -1;
                if (playerRB.velocity.y < 0)
                {
                    playerRB.velocity = new Vector2(playerRB.velocity.x, 0);
                }
            }
        }
        

    }

    void OnCollisionStay2D(Collision2D coll)
    {
        //if (coll.transform.tag == "Platform")
        //{
        //    //transform.parent = coll.transform;
        //    HorizontalMovingPlatform plat = coll.gameObject.GetComponent<HorizontalMovingPlatform>();
        //    playerRB.velocity = new Vector2(playerRB.velocity.x + plat.platVelocity.x, playerRB.velocity.y + plat.platVelocity.y);
        //}
    }

    void OnCollisionExit2D(Collision2D coll)
    {
        if (coll.gameObject.CompareTag("Enemy") || coll.gameObject.CompareTag("EnemyHurtbox"))
        {
            return;
        }
        if (coll.transform.tag == "Platform")
        {
            //transform.parent = level;
            baseVelocity = 0;
            plat = null;
        }
        surfacesTouching -= 1;
        touching.Remove(coll.collider);
    }
    #endregion

    #region MoveFunctions
    void moveFunction()
    {
        float vel_direction = playerRB.velocity.x != 0 ? playerRB.velocity.x / Mathf.Abs(playerRB.velocity.x) : 0;
        if (vel_direction != xInput || xInput == 0)
        {
            playerRB.velocity = new Vector2(baseVelocity + xInput * speed, playerRB.velocity.y);
        }
        if (surfacesTouching == 0)
        {
            if (Mathf.Abs(playerRB.velocity.x) < maxAirSpeedToTurnAround)
            {
                playerRB.AddForce(new Vector2(xInput * airSpeed, 0));
            }

        }
        else if (Mathf.Abs(xInput * 70) > Mathf.Abs(playerRB.velocity.x))
        {
            Vector2 movementVector = new Vector2(xInput * speed + baseVelocity, playerRB.velocity.y);
            playerRB.velocity = movementVector;
        }
    }

    void wallJumpMove()
    {
        playerRB.AddForce(new Vector2(xInput * wallJumpDrift, 0));
    }

    IEnumerator Dash()
    {
        long prev_state = transition(new pState[] { pState.nothing, pState.wallJumping, pState.jumping }, pState.dashing);
        if (prev_state != 0)
        {
            FindObjectOfType<AudioManager>().Play("Dash");
            playerRB.gravityScale = 0;
            playerRB.velocity = new Vector2(dashSpeed * xInput, dashSpeed * yInput);
            yield return new WaitForSeconds(secondsToWaitForDash);
            playerRB.velocity = new Vector2(speed * xInput, speed * yInput);
            playerRB.gravityScale = trueGrav;
            dashes -= 1;
            transition_out(prev_state, pState.nothing);
        }

    }

    IEnumerator wallJump()
    {
        long prev_state = transition(new pState[] { pState.nothing, pState.wallJumping, pState.jumping, pState.attacking }, pState.wallJumping);
        if (prev_state != 0)
        {
            FindObjectOfType<AudioManager>().Play("Jump");
            int direction = 0;
            if (rightWallRiding) direction = -1;
            if (leftWallRiding) direction = 1;
            playerRB.velocity = new Vector2(wallJumpForce * direction, wallJumpForce * 2f);
            yield return new WaitForSeconds(secondsToWaitForWallJump * 0.6f);
            wallJumpInfluence = true;
            yield return new WaitForSeconds(secondsToWaitForWallJump * 0.4f);
            wallJumpInfluence = false;
            transition_out(prev_state, pState.nothing);
        }

    }

    //IEnumerator Jump()
    //{
    //    long prev_state = transition(new pState[] { pState.nothing, pState.attacking, pState.wallJumping }, pState.jumping);
    //    if (prev_state != 0)
    //    {
    //        playerRB.velocity = new Vector2(playerRB.velocity.x, jumpforce);
    //        playerRB.gravityScale = 0;
    //        for (int i = 0; i < 3; i++)
    //        {
    //            yield return new WaitForSeconds(0.05f);
    //            if (!((Input.GetKey(KeyCode.Space) && state == pState.jumping)))
    //            {
    //                if (transition_out(prev_state, pState.nothing))
    //                {
    //                    playerRB.gravityScale = trueGrav;
    //                }
    //                yield return null;
    //            }
    //        }
    //        if (transition_out(prev_state, pState.nothing))
    //        {
    //            playerRB.gravityScale = trueGrav;

    //        }
    //        yield return null;
    //    }
    //}
    //IEnumerator Jump()
    //{
    //    if (!timePowerOn)
    //    {
    //        secondsToWaitForJump = initialSTWFJ;
    //    }

    //    int lengthHold = 1;
    //    //if (timePowerOn)
    //    //{
    //    //    lengthHold = 5;
    //    //}

    //    long prev_state = transition(new pState[] { pState.nothing, pState.attacking, pState.wallJumping }, pState.jumping);
    //    if (prev_state != 0)
    //    {
    //        playerRB.AddForce(new Vector2(0, jumpforce));
    //        for (int i = 0; i < 3; i++)
    //        {
    //            yield return new WaitForSeconds(secondsToWaitForJump);
    //            if (!((Input.GetKey(KeyCode.Space) && state == pState.jumping)))
    //            {
    //                playerRB.AddForce(new Vector2(0, -1 * jumpforce / 3));
    //                transition_out(prev_state, pState.nothing);
    //                yield return null;
    //            } else
    //            {
    //                playerRB.AddForce(new Vector2(0, jumpforce / lengthHold));
    //                lengthHold *= 2;
    //            }
    //        }
    //        transition_out(prev_state, pState.nothing);
    //        yield return null;
    //    }
    //}

    IEnumerator Jump()
    {
        long prev_state = transition(new pState[] { pState.nothing, pState.attacking, pState.wallJumping }, pState.jumping);
        if (prev_state != 0)
        {
            FindObjectOfType<AudioManager>().Play("Jump");
            playerRB.AddForce(new Vector2(0, jumpforce));
            yield return new WaitForSeconds(secondsToWaitForJump);
            transition_out(prev_state, pState.nothing);
        }
        yield return null;
    }

    #endregion

    #region AttackFunctions
    IEnumerator Attack()
    {
        if (state == pState.wallJumping)
        {
            walljumpAttack = true;
        }
        long prev_state = transition(pState.nothing, pState.attacking) | transition(pState.wallJumping, pState.attacking);
        if (prev_state != 0)
        {
            yield return new WaitForSeconds(hitboxTiming);
            FindObjectOfType<AudioManager>().Play("Attack");
            Debug.Log("Cast hitbox now");

            //Create hitbox
            RaycastHit2D[] hits = Physics2D.BoxCastAll(playerRB.position + new Vector2(dirFacing * 10, 0), new Vector2(10.0f, 10.0f), 0f, Vector2.zero, 0);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.transform.CompareTag("Projectile"))
                {
                    hit.transform.GetComponent<Projectile>().ReflectProjectile();
                    hit.transform.Rotate(0, 180, 0);

                }
                if (hit.transform.CompareTag("EnemyHurtbox"))
                {
                    Debug.Log("tons of damage");
                    hit.transform.GetComponent<EnemyHurtbox>().TakeDamage(damage);
                }
                if (hit.transform.CompareTag("BossHurtbox"))
                {
                    Debug.Log("tons of damage");
                    hit.transform.GetComponent<BossHurtbox>().TakeDamage(damage);
                }
                if (hit.transform.CompareTag("QueenHurtbox"))
                {
                    Debug.Log("tons of damage");
                    hit.transform.GetComponent<QueenHurtbox>().TakeDamage(damage);
                }
                if (hit.transform.CompareTag("FinalChoiceHurtbox"))
                {
                    Debug.Log("tons of damage");
                    hit.transform.GetComponent<FinalChoiceHurtbox>().TakeDamage(damage);
                }
            }

            yield return new WaitForSeconds(attackAnimationTiming);
            walljumpAttack = false;
            transition_out(prev_state, pState.nothing);
        }
        yield return null;

    }
    #endregion

    #region TimeFunctions
    private void TimePower()
    {
        timePowerOn = !timePowerOn;

        if (timePowerOn)
        {
            timeBuffer.setBuffer();
            StartCoroutine("FreezeForTimePower");
            StartCoroutine("SlowTimeForAllButPlayer");
            //SlowTimeForAllButPlayer();
        }
        else
        {
            timeBuffer.setBuffer();
            StartCoroutine("FreezeForTimePower");
            StartCoroutine("ResumeTimeForAll");
            //ResumeTimeForAll();
        }
    }

    IEnumerator SlowTimeForAllButPlayer()
    {
        //Slows down the speed of the enemy's walking speed and the block's falling speed
        Time.timeScale = 0.25f;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        speed *= 4;
        jumpforce *= 9f;
        Debug.Log(playerRB.gravityScale);
        playerRB.gravityScale *= 6.5f;
        trueGrav *= 6.5f;
        secondsToWaitForDash = .1f;

        secondsToWaitForJump = 0.1f;
        wallJumpForce *= 2;
        secondsToWaitForWallJump = 0.2f;

        yield return null;
    }

    //private void SlowTimeForAllButPlayer()
    //{
    //    //Slows down the speed of the enemy's walking speed and the block's falling speed
    //    Time.timeScale = 0.25f;
    //    Time.fixedDeltaTime = 0.02f * Time.timeScale;
    //    speed *= 4;
    //    jumpforce *= 9f;
    //    Debug.Log(playerRB.gravityScale);
    //    playerRB.gravityScale *= 6.5f;
    //    trueGrav *= 6.5f;
    //    secondsToWaitForDash = .1f;

    //    secondsToWaitForJump = 0.1f;
    //    wallJumpForce *= 2;
    //    secondsToWaitForWallJump = 0.2f;
    //}

    IEnumerator ResumeTimeForAll()
    {
        //Resets the speeds of the enemy and falling block
        Time.timeScale = 1;
        Time.fixedDeltaTime = 0.02f * Time.timeScale;

        speed = speed / 4;
        jumpforce = jumpforce / 9f;
        playerRB.gravityScale = playerRB.gravityScale / 6.5f;
        trueGrav = trueGrav / 6.5f;
        secondsToWaitForDash = 0.15f;
        dashSpeed = speed + 90;
        secondsToWaitForJump = 0.3f;
        wallJumpForce = wallJumpForce / 2;
        secondsToWaitForWallJump = 0.4f;
        yield return null;
    }

    //private void ResumeTimeForAll()
    //{
    //    //Resets the speeds of the enemy and falling block
    //    Time.timeScale = 1;
    //    Time.fixedDeltaTime = 0.02f * Time.timeScale;

    //    speed = speed / 4;
    //    jumpforce = jumpforce / 8.5f;
    //    playerRB.gravityScale = playerRB.gravityScale / 6.5f;
    //    trueGrav = trueGrav / 6.5f;
    //    secondsToWaitForDash = 0.15f;
    //    dashSpeed = speed + 90;
    //    secondsToWaitForJump = 0.3f;
    //    wallJumpForce = wallJumpForce / 2;
    //    secondsToWaitForWallJump = 0.4f;

    //}


    IEnumerator FreezeForTimePower()
    {
        if (timePowerOn)
        {
            bufferSeconds = 0.125f;
        }
        else
        {
            bufferSeconds = 0.5f;
        }
        playerRB.constraints = RigidbodyConstraints2D.FreezePosition;
        yield return new WaitForSeconds(bufferSeconds);
        playerRB.constraints = originalConstraints;

        //Uncomment this for some fun ;)
        //playerRB.constraints = RigidbodyConstraints2D.None;
        timeBuffer.setBuffer();
    }


    #endregion

    #region DialogueFunctions
    public void enterDialogue()
    {
        Debug.Log("Entering Dialogue");
        inDialogue = true;
    }

    public void exitDialogue()
    {
        inDialogue = false;
    }

    #endregion

    #region PlayerState
    public enum pState
    {
        wallJumping,
        nothing,
        dashing,
        attacking,
        jumping,
        respawning
    }

    public pState state = pState.nothing;
    long state_id = 1;
    long transition(pState before, pState after)
    {
        if (state == before)
        {
            state = after;
            return ++state_id;
        } else
        {
            return 0;
        }
    }
    long transition(pState[] befores, pState after)
    {
        foreach (pState before in befores)
        {
            if (state == before)
            {
                state = after;
                return ++state_id;
            }
        }
        return 0;
    }
    bool transition_out(long id, pState dest)
    {
        if (state_id == id)
        {
            state = dest;
            return true;
        }
        return false;
    }

    //Function for player to die.
    private void die()
    {
        FindObjectOfType<AudioManager>().Play("Kill");
        MakeVulnerable();
        ResumeTimeForAll();
        if (timePowerOn)
        {
            TimePower();
        }
        //GameObject gc = GameObject.FindWithTag("GameController");
        if (respawnBool)
        {
            respawn();
        } else
        {
            //gc.GetComponent<GameManager1>().EndGame();
            GameManager1.instance.EndGame();
        }

    }
    //Function for the player to respawn at the starting position of the screen
    private void respawn()
    {
        StartCoroutine("Respawn");
    }
    public IEnumerator Respawn()
    {
        long prev_state = transition(new pState[] { pState.nothing, pState.attacking, pState.wallJumping, pState.dashing, pState.jumping}, pState.respawning);
        if (prev_state != 0)
        {
            playerRB.bodyType = RigidbodyType2D.Static;
            yield return new WaitForSeconds(0.2f);
            while (Mathf.Abs(gameObject.transform.position.x - respawnPos.x) > 1 ||
                Mathf.Abs(gameObject.transform.position.y - respawnPos.y) > 1 ||
                Mathf.Abs(gameObject.transform.position.z - respawnPos.z) > 1)
            {
                gameObject.transform.position = Vector3.MoveTowards(gameObject.transform.position, respawnPos, 10.0f);
                yield return new WaitForEndOfFrame();
                print("loop");
            }
            timeHealthValue = 100;
            playerRB.bodyType = RigidbodyType2D.Dynamic;
            transition_out(prev_state, pState.nothing);
        }
        yield return null;
    }
    #endregion

    public void TakeDamage(float knockbackForce, Vector3 enemyPos, float damage)
    {
        if (!takingDamage)
        {
            timeHealthValue -= damage;
            timeBar.value = timeHealthValue;
            takingDamage = true;
            takingDamageStun = true;
            StartCoroutine(TakeDamageCoroutine(knockbackForce, enemyPos));
        }
    }

    public void MakeVulnerable()
    {
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, false);
        spriteRenderer.material.color = originalColor;
        takingDamage = false;
        takingDamageStun = false;
    }

    public IEnumerator TakeDamageCoroutine(float knockbackForce, Vector3 enemyPos)
    {
        
        // Flash
        spriteRenderer.material.color = Color.red;

        // Remove collision rule
        Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, true);

        // Apply knockback
        playerRB.velocity = Vector3.zero;
        float sign = Mathf.Sign(transform.position.x - enemyPos.x);
        playerRB.AddForce(Vector2.up * jumpforce/2.0f);
        
        float timeElapse = 0;
        while (timeElapse < invulnerabilityTime * 0.25)
        {
            // Constant horizontal "force", more consistent than AddForce
            playerRB.velocity = Vector2.right * knockbackForce * sign;
            timeElapse += Time.deltaTime;
            yield return null;
        }

        takingDamageStun = false;
        playerRB.gravityScale = trueGrav;
        //rend.material.color = Color.blue;
        
        Color inv = originalColor;
        inv.a = 0.5f;
        spriteRenderer.material.color = inv;
        yield return new WaitForSecondsRealtime(invulnerabilityTime * 0.75f);

        // Revert
        MakeVulnerable();
    }
}
