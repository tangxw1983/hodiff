UPDATE 
sl_invest_wp a, ct_race b, ct_card c, ct_race_odds d
SET a.w_result = d.odds_value
WHERE a.`cd_id` = c.`id` AND c.`tournament_id` = b.`tournament_id` AND b.`race_no` = a.`rc_no` AND d.`tote_type` = c.`tote_type`
AND d.`race_id` = b.`id` AND d.`hrs_no` = CONCAT('', a.`hs_no`) AND d.`odds_type` = 'WIN';

UPDATE 
sl_invest_wp a, ct_race b, ct_card c, ct_race_odds d
SET a.p_result = d.odds_value
WHERE a.`cd_id` = c.`id` AND c.`tournament_id` = b.`tournament_id` AND b.`race_no` = a.`rc_no` AND d.`tote_type` = c.`tote_type`
AND d.`race_id` = b.`id` AND d.`hrs_no` = CONCAT('', a.`hs_no`) AND d.`odds_type` = 'PLC';

UPDATE
sl_invest_qn a, ct_race b, ct_card c, ct_race_odds d
SET a.`result` = d.`odds_value`
WHERE a.`cd_id` = c.`id` AND c.`tournament_id` = b.`tournament_id` AND b.`race_no` = a.`rc_no` AND d.`tote_type` = c.`tote_type`
AND d.`race_id` = b.`id` AND d.`hrs_no` = a.`hs_no` AND a.`q_type` = 'Q' AND d.`odds_type` = 'FC';

UPDATE
sl_invest_qn a, ct_race b, ct_card c, ct_race_odds d
SET a.`result` = d.`odds_value`
WHERE a.`cd_id` = c.`id` AND c.`tournament_id` = b.`tournament_id` AND b.`race_no` = a.`rc_no` AND d.`tote_type` = c.`tote_type`
AND d.`race_id` = b.`id` AND d.`hrs_no` = a.`hs_no` AND a.`q_type` = 'QP' AND d.`odds_type` = 'PFT';