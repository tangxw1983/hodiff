SELECT
 direction, cd_id,
 SUM(w_amt * percent / 100) bw, SUM(p_amt * percent / 100) bp,
 SUM(w_amt * (CASE WHEN w_limit < IFNULL(w_result,0) THEN w_limit ELSE IFNULL(w_result,0) END) / 10) ww,
 SUM(p_amt * (CASE WHEN p_limit < IFNULL(p_result,0) THEN p_limit ELSE IFNULL(p_result,0) END) / 10) wp,
 SUM(w_amt * (CASE WHEN w_result > 0 THEN CASE WHEN w_limit < w_od * 10 THEN w_limit ELSE w_od * 10 END ELSE 0 END) / 10) pw,
 SUM(p_amt * (CASE WHEN p_result > 0 THEN CASE WHEN p_limit < p_od * 10 THEN p_limit ELSE p_od * 10 END ELSE 0 END) / 10) pp
FROM `sl_invest_wp`  
WHERE model = 104 AND cd_id IN (SELECT id FROM ct_card WHERE tote_type = 'HK')
GROUP BY direction, cd_id WITH ROLLUP;

SELECT 
direction, q_type, cd_id,
SUM(amt * percent / 100) bet,
SUM(amt * (CASE WHEN q_limit < IFNULL(result,0) THEN q_limit ELSE IFNULL(result,0) END) / 10) won,
SUM(amt * (CASE WHEN result > 0 THEN CASE WHEN q_limit < od * 10 THEN q_limit ELSE od * 10 END ELSE 0 END) / 10) predict
FROM `sl_invest_qn`
WHERE model = 104 AND percent < 100
GROUP BY direction, q_type, cd_id WITH ROLLUP;
